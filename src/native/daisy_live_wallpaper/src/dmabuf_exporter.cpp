#include "dmabuf_exporter.h"
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <unistd.h>
#include <fcntl.h>
#include <sys/stat.h>

extern "C" {
#include <libavutil/hwcontext.h>
#include <libavutil/hwcontext_drm.h>
#include <libavutil/imgutils.h>
#include <libswscale/swscale.h>
#include <drm_fourcc.h>
}

namespace daisy {

DmabufExporter::~DmabufExporter() {
    reset_sws_context();
}

void DmabufExporter::reset_sws_context() {
    if (sws_ctx_) {
        sws_freeContext(sws_ctx_);
        sws_ctx_ = nullptr;
    }
    sws_src_w_ = 0;
    sws_src_h_ = 0;
    sws_src_fmt_ = -1;
}

void DmabufExporter::init(AVHWDeviceType hw_type, StatsCollector &stats) {
    reset_sws_context();
    // Detect multi-GPU topology once per session (§3)
    struct stat render_st{}, card_st{};
    bool has_render = (stat("/dev/dri/renderD128", &render_st) == 0);
    bool has_card   = (stat("/dev/dri/card0", &card_st) == 0);

    if (has_render && has_card && (render_st.st_rdev != card_st.st_rdev)) {
        topology_mode_ = TopologyMode::CrossDeviceDmabuf;
    } else {
        topology_mode_ = TopologyMode::SameDeviceZeroCopy;
    }

    sync_mode_ = SyncMode::Explicit;

    std::fprintf(stderr, "[daisy-dmabuf] Topology: %s, Sync: %s\n",
                 topology_string(), sync_mode_string());
}

const char *DmabufExporter::topology_string() const {
    switch (topology_mode_) {
        case TopologyMode::SameDeviceZeroCopy: return "same-device zero-copy";
        case TopologyMode::CrossDeviceDmabuf:   return "cross-device dmabuf";
        case TopologyMode::CrossDeviceGpuCopy:  return "cross-device gpu-copy";
    }
    return "same-device zero-copy";
}

const char *DmabufExporter::sync_mode_string() const {
    switch (sync_mode_) {
        case SyncMode::Explicit:       return "explicit";
        case SyncMode::ImplicitPolled: return "implicit-polled";
    }
    return "explicit";
}

bool DmabufExporter::export_frame(AVFrame *src_frame, DaisyVideoFrame *out, StatsCollector &stats) {
    if (!src_frame || !out) return false;

    std::memset(out, 0, sizeof(*out));
    for (int i = 0; i < DAISY_MAX_PLANES; ++i) out->fd[i] = -1;
    out->acquire_fence = -1;
    out->width  = src_frame->width;
    out->height = src_frame->height;

    if (src_frame->pts != AV_NOPTS_VALUE) {
        out->pts_us = src_frame->pts;
    } else {
        out->pts_us = 0;
    }

    AVFrame *sw_frame = src_frame;
    AVFrame *mapped_sw_frame = nullptr;

    // Hardware frame path (VA-API, CUDA/NVDEC, DRM PRIME)
    if (src_frame->format == AV_PIX_FMT_DRM_PRIME || src_frame->format == AV_PIX_FMT_VAAPI || src_frame->format == AV_PIX_FMT_CUDA) {
        AVFrame *drm_frame = nullptr;
        bool need_free_drm = false;

        if (src_frame->format == AV_PIX_FMT_DRM_PRIME) {
            drm_frame = src_frame;
        } else {
            drm_frame = av_frame_alloc();
            if (drm_frame) {
                drm_frame->format = AV_PIX_FMT_DRM_PRIME;
                int err = av_hwframe_map(drm_frame, src_frame, AV_HWFRAME_MAP_READ);
                if (err < 0) {
                    av_frame_free(&drm_frame);
                    drm_frame = nullptr;
                } else {
                    need_free_drm = true;
                }
            }
        }

        if (drm_frame && drm_frame->data[0]) {
            const AVDRMFrameDescriptor *desc = reinterpret_cast<const AVDRMFrameDescriptor *>(drm_frame->data[0]);
            if (desc && desc->nb_layers > 0) {
                out->pixel_format = desc->layers[0].format;
                out->is_hardware  = 1;

                int plane_idx = 0;
                for (int l = 0; l < desc->nb_layers && plane_idx < DAISY_MAX_PLANES; ++l) {
                    const AVDRMLayerDescriptor &layer = desc->layers[l];
                    for (int p = 0; p < layer.nb_planes && plane_idx < DAISY_MAX_PLANES; ++p) {
                        const AVDRMPlaneDescriptor &plane = layer.planes[p];
                        const AVDRMObjectDescriptor &obj  = desc->objects[plane.object_index];

                        out->fd[plane_idx]     = dup(obj.fd);
                        out->stride[plane_idx] = plane.pitch;
                        out->offset[plane_idx] = plane.offset;
                        out->modifier          = obj.format_modifier;
                        plane_idx++;
                    }
                }
                out->n_planes = plane_idx;
                stats.zero_copy     = 1;
                stats.dmabuf_active = 1;
            }
        }

        if (need_free_drm && drm_frame) av_frame_free(&drm_frame);

        // Also transfer software frame for UI fallback rendering
        mapped_sw_frame = av_frame_alloc();
        if (mapped_sw_frame) {
            if (av_hwframe_transfer_data(mapped_sw_frame, src_frame, 0) == 0) {
                sw_frame = mapped_sw_frame;
            } else {
                av_frame_free(&mapped_sw_frame);
                mapped_sw_frame = nullptr;
            }
        }
    }

    // Convert sw_frame to BGRA32 buffer for UI presentation
    if (sw_frame && sw_frame->data[0]) {
        int buf_size = out->width * out->height * 4;
        uint8_t *bgra_buffer = static_cast<uint8_t *>(std::malloc(buf_size));
        if (bgra_buffer) {
            int src_fmt = static_cast<int>(sw_frame->format);
            if (!sws_ctx_ || sws_src_w_ != out->width || sws_src_h_ != out->height || sws_src_fmt_ != src_fmt) {
                if (sws_ctx_) sws_freeContext(sws_ctx_);
                sws_ctx_ = sws_getContext(
                    out->width, out->height, static_cast<AVPixelFormat>(src_fmt),
                    out->width, out->height, AV_PIX_FMT_BGRA,
                    SWS_FAST_BILINEAR, nullptr, nullptr, nullptr);
                sws_src_w_   = out->width;
                sws_src_h_   = out->height;
                sws_src_fmt_ = src_fmt;
            }

            if (sws_ctx_) {
                uint8_t *dst[4] = { bgra_buffer, nullptr, nullptr, nullptr };
                int dst_stride[4] = { out->width * 4, 0, 0, 0 };
                sws_scale(sws_ctx_, sw_frame->data, sw_frame->linesize, 0, out->height, dst, dst_stride);

                // Store BGRA pointer in stride[0]/stride[1] (with fd[0] = -2 sentinel if pure SW or software buffer accessor)
                uintptr_t ptr = reinterpret_cast<uintptr_t>(bgra_buffer);
                if (out->is_hardware == 0) {
                    out->fd[0] = -2;
                    out->pixel_format = DRM_FORMAT_ARGB8888;
                    out->n_planes = 1;
                    out->modifier = DRM_FORMAT_MOD_LINEAR;
                    stats.zero_copy = 0;
                    stats.dmabuf_active = 0;
                    stats.cpu_fallback_count.fetch_add(1, std::memory_order_relaxed);
                }
                out->stride[0] = static_cast<uint32_t>(ptr & 0xFFFFFFFF);
                out->stride[1] = static_cast<uint32_t>((ptr >> 32) & 0xFFFFFFFF);
                out->stride[2] = static_cast<uint32_t>(out->width * 4);
                out->stride[3] = static_cast<uint32_t>(out->height);
            } else {
                std::free(bgra_buffer);
            }
        }
    }

    if (mapped_sw_frame) av_frame_free(&mapped_sw_frame);
    return true;
}

} // namespace daisy
