/// Standalone test for the daisy_live_wallpaper native library.
/// Usage: daisy_wallpaper_test <video_file> [frame_count]
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <ctime>
#include <unistd.h>
#include "../include/daisy/live_wallpaper.h"

int main(int argc, char *argv[]) {
    if (argc < 2) {
        std::fprintf(stderr, "Usage: %s <video_file> [frame_count]\n", argv[0]);
        return 1;
    }
    const char *path  = argv[1];
    int max_frames    = (argc >= 3) ? std::atoi(argv[2]) : 60;
    if (max_frames <= 0) max_frames = 60;

    std::fprintf(stderr, "[test] Opening: %s (want %d frames)\n", path, max_frames);

    DaisyWallpaperHandle h = daisy_wallpaper_create();
    if (!h) { std::fprintf(stderr, "[test] create failed\n"); return 1; }

    int ret = daisy_wallpaper_load(h, path);
    if (ret != 0) {
        std::fprintf(stderr, "[test] load failed: %d\n", ret);
        daisy_wallpaper_destroy(h);
        return 1;
    }

    daisy_wallpaper_set_fps_cap(h, 60);
    daisy_wallpaper_play(h);

    int received = 0, polls = 0;
    const int max_polls = max_frames * 40;

    while (received < max_frames && polls < max_polls) {
        DaisyVideoFrame frame{};
        for (int i = 0; i < DAISY_MAX_PLANES; ++i) frame.fd[i] = -1;
        frame.acquire_fence = -1;

        if (daisy_wallpaper_try_get_frame(h, &frame)) {
            std::fprintf(stdout,
                "Frame %3d: pts=%7lld us  %dx%d  fourcc=0x%08x  hw=%d  "
                "planes=%d  fds=[%d,%d,%d,%d]  strides=[%u,%u,%u,%u]\n",
                received, (long long)frame.pts_us,
                frame.width, frame.height, frame.pixel_format,
                frame.is_hardware, frame.n_planes,
                frame.fd[0], frame.fd[1], frame.fd[2], frame.fd[3],
                frame.stride[0], frame.stride[1], frame.stride[2], frame.stride[3]);

            // Close DMA-BUF FDs
            for (int i = 0; i < DAISY_MAX_PLANES; ++i) {
                if (frame.fd[i] >= 0) { ::close(frame.fd[i]); frame.fd[i] = -1; }
            }
            if (frame.acquire_fence >= 0) { ::close(frame.acquire_fence); frame.acquire_fence = -1; }

            // Free CPU buffer (software path sentinel: fd[0]==-2)
            if (frame.fd[0] == -2) {
                uintptr_t ptr = (uintptr_t)frame.stride[0] | ((uintptr_t)frame.stride[1] << 32);
                if (ptr) std::free(reinterpret_cast<void *>(ptr));
            }
            ++received;
        } else {
            struct timespec ts{0, 2000000}; // 2ms
            nanosleep(&ts, nullptr);
        }
        ++polls;
    }

    daisy_wallpaper_stop(h);
    daisy_wallpaper_print_stats(h);
    daisy_wallpaper_destroy(h);

    std::fprintf(stderr, "[test] Done: %d frames in %d polls\n", received, polls);
    return received > 0 ? 0 : 1;
}
