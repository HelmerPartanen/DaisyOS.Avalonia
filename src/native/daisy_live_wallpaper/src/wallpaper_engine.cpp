#include "wallpaper_engine.h"
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <chrono>
#include <algorithm>
#include <unistd.h>
#include <drm_fourcc.h>

namespace daisy {

WallpaperEngine::WallpaperEngine() = default;

WallpaperEngine::~WallpaperEngine() {
    stop();
}

int WallpaperEngine::load(const std::string &path) {
    stop();

    path_ = path;
    int ret = decoder_.open(path_, stats_);
    if (ret < 0) {
        std::fprintf(stderr, "[daisy-engine] Failed to open decoder for %s: %d\n", path.c_str(), ret);
        return ret;
    }

    exporter_.init(decoder_.info().hw_type, stats_);
    stats_.is_playing = 0;
    return 0;
}

void WallpaperEngine::play() {
    if (is_running_) {
        is_paused_ = false;
        stats_.is_playing = 1;
        queue_cv_.notify_all();
        return;
    }

    stop_requested_ = false;
    is_paused_      = false;
    is_running_     = true;
    stats_.is_playing = 1;

    decode_thread_ = std::thread(&WallpaperEngine::decoder_thread_loop, this);
}

void WallpaperEngine::pause() {
    is_paused_        = true;
    stats_.is_playing = 0;
    clear_frame_queue();
    queue_cv_.notify_all();
}

void WallpaperEngine::stop() {
    stop_requested_ = true;
    is_paused_      = false;
    queue_cv_.notify_all();

    if (decode_thread_.joinable()) {
        decode_thread_.join();
    }

    is_running_       = false;
    stats_.is_playing = 0;
    clear_frame_queue();
    decoder_.close();
}

void WallpaperEngine::set_fps_cap(int fps) {
    if (fps <= 0) fps = 60;
    fps_cap_ = fps;
}

void WallpaperEngine::clear_frame_queue() {
    std::lock_guard<std::mutex> lock(queue_mutex_);
    while (!frame_queue_.empty()) {
        DaisyVideoFrame f = frame_queue_.front();
        frame_queue_.pop();
        for (int i = 0; i < DAISY_MAX_PLANES; ++i) {
            if (f.fd[i] >= 0) ::close(f.fd[i]);
        }
        if (f.acquire_fence >= 0) ::close(f.acquire_fence);
        if (f.fd[0] == -2) {
            uintptr_t ptr = static_cast<uintptr_t>(f.stride[0]) | (static_cast<uintptr_t>(f.stride[1]) << 32);
            if (ptr) std::free(reinterpret_cast<void *>(ptr));
        }
    }
    stats_.queue_depth = 0;
}

void WallpaperEngine::decoder_thread_loop() {
    using clock = std::chrono::steady_clock;
    auto last_target_time = clock::now();

    while (!stop_requested_) {
        {
            std::unique_lock<std::mutex> lock(queue_mutex_);
            queue_cv_.wait(lock, [this] {
                return stop_requested_ || !is_paused_;
            });
        }

        if (stop_requested_) {
            break;
        }

        double src_fps = stats_.src_fps > 0 ? stats_.src_fps : 30.0;
        int cap = fps_cap_.load();
        double effective_fps = (cap > 0 && cap < src_fps) ? cap : src_fps;
        auto frame_interval = std::chrono::microseconds(static_cast<int64_t>(1000000.0 / effective_fps));

        bool eof = false;
        AVFrame *raw_frame = decoder_.decode_next_frame(&eof);

        if (eof || !raw_frame) {
            // Seamless Looping (§12): seek to start without dropping current buffer
            decoder_.seek_to_start();
            if (raw_frame) av_frame_free(&raw_frame);
            continue;
        }

        DaisyVideoFrame frame{};
        bool exported = exporter_.export_frame(raw_frame, &frame, stats_);
        av_frame_free(&raw_frame);

        if (!exported) {
            std::this_thread::sleep_for(std::chrono::milliseconds(5));
            continue;
        }

        {
            std::unique_lock<std::mutex> lock(queue_mutex_);
            if (frame_queue_.size() >= kMaxQueueDepth) {
                DaisyVideoFrame oldest = frame_queue_.front();
                frame_queue_.pop();
                for (int i = 0; i < DAISY_MAX_PLANES; ++i) {
                    if (oldest.fd[i] >= 0) ::close(oldest.fd[i]);
                }
                if (oldest.acquire_fence >= 0) ::close(oldest.acquire_fence);
                if (oldest.fd[0] == -2) {
                    uintptr_t ptr = static_cast<uintptr_t>(oldest.stride[0]) | (static_cast<uintptr_t>(oldest.stride[1]) << 32);
                    if (ptr) std::free(reinterpret_cast<void *>(ptr));
                }
                stats_.dropped_frames.fetch_add(1, std::memory_order_relaxed);
            }

            frame_queue_.push(frame);
            stats_.queue_depth = static_cast<int>(frame_queue_.size());
            stats_.decoded_frames.fetch_add(1, std::memory_order_relaxed);
        }

        // Pacing frame rate
        last_target_time += frame_interval;
        auto now = clock::now();
        if (now < last_target_time) {
            std::this_thread::sleep_for(last_target_time - now);
        } else {
            last_target_time = now;
        }
    }
}

bool WallpaperEngine::try_get_frame(DaisyVideoFrame *out) {
    if (!out) return false;

    std::lock_guard<std::mutex> lock(queue_mutex_);
    if (frame_queue_.empty()) return false;

    *out = frame_queue_.front();
    frame_queue_.pop();
    stats_.queue_depth = static_cast<int>(frame_queue_.size());
    return true;
}

void WallpaperEngine::get_stats(DaisyWallpaperStats *out) const {
    if (!out) return;

    std::snprintf(out->decoder_backend, sizeof(out->decoder_backend), "%s", stats_.decoder_backend);
    std::snprintf(out->codec, sizeof(out->codec), "%s", stats_.codec);
    out->src_width          = stats_.src_width;
    out->src_height         = stats_.src_height;
    out->src_fps            = stats_.src_fps;
    out->displayed_fps      = stats_.src_fps;
    out->decoded_frames     = stats_.decoded_frames.load(std::memory_order_relaxed);
    out->dropped_frames     = stats_.dropped_frames.load(std::memory_order_relaxed);
    out->decode_time_us     = stats_.decode_time_us.load(std::memory_order_relaxed);
    out->queue_depth        = stats_.queue_depth;
    out->zero_copy          = stats_.zero_copy;
    out->dmabuf_active      = stats_.dmabuf_active;
    out->egl_import_active  = stats_.egl_import_active;
    out->drm_overlay        = stats_.drm_overlay;
    out->cpu_fallback_count = stats_.cpu_fallback_count.load(std::memory_order_relaxed);
    out->gpu_copy_fallback  = stats_.gpu_copy_fallback.load(std::memory_order_relaxed);
    out->is_playing         = stats_.is_playing;
}

void WallpaperEngine::print_stats() const {
    DaisyWallpaperStats s{};
    get_stats(&s);

    const char *format_str = (s.zero_copy && s.dmabuf_active) ? "NV12" : "NV12 (SW)";

    std::fprintf(stderr,
        "\nLiveWallpaper:\n"
        "  codec: %s\n"
        "  resolution: %dx%d\n"
        "  source fps: %.2f\n"
        "  decoder: %s\n"
        "  gpu-device-topology: %s\n"
        "  zero-copy: %s\n"
        "  format: %s\n"
        "  dmabuf: %s\n"
        "  sync-mode: %s\n"
        "  presentation: %s\n"
        "  queued frames: %d\n"
        "  dropped frames: %d\n"
        "  audio: not opened\n\n",
        s.codec,
        s.src_width, s.src_height,
        s.src_fps,
        s.decoder_backend,
        exporter_.topology_string(),
        s.zero_copy ? "yes" : "no",
        format_str,
        s.dmabuf_active ? "yes" : "no",
        exporter_.sync_mode_string(),
        s.drm_overlay ? "DRM overlay" : "Vulkan compositor",
        s.queue_depth,
        s.dropped_frames
    );
}

} // namespace daisy
