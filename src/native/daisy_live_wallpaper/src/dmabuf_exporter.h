#ifndef DAISY_DMABUF_EXPORTER_H
#define DAISY_DMABUF_EXPORTER_H

#include "../include/daisy/live_wallpaper.h"
#include "hw_decoder.h"

extern "C" {
#include <libavutil/frame.h>
#include <libavutil/hwcontext_drm.h>
}

namespace daisy {

enum class TopologyMode {
    SameDeviceZeroCopy,
    CrossDeviceDmabuf,
    CrossDeviceGpuCopy
};

enum class SyncMode {
    Explicit,
    ImplicitPolled
};

class DmabufExporter {
public:
    DmabufExporter() = default;
    ~DmabufExporter() = default;

    void init(AVHWDeviceType hw_type, StatsCollector &stats);

    bool export_frame(AVFrame *src_frame, DaisyVideoFrame *out_frame, StatsCollector &stats);

    TopologyMode topology_mode() const { return topology_mode_; }
    SyncMode sync_mode() const { return sync_mode_; }
    const char *topology_string() const;
    const char *sync_mode_string() const;

private:
    TopologyMode topology_mode_ = TopologyMode::SameDeviceZeroCopy;
    SyncMode     sync_mode_     = SyncMode::Explicit;
    uint64_t     negotiated_modifier_ = 0; // DRM_FORMAT_MOD_INVALID
};

} // namespace daisy

#endif // DAISY_DMABUF_EXPORTER_H
