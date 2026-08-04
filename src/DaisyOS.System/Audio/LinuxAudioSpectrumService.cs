using System.Diagnostics;
using DaisyOS.Core.Services;

namespace DaisyOS.System.Audio;

public sealed class LinuxAudioSpectrumService : IAudioSpectrumService
{
    private const int BarCount = 6;
    private const int SampleRate = 16_000;
    private const int SampleCount = 512;
    private const int BytesPerSample = 2;
    private const double SilenceThreshold = 0.004;
    private readonly object _syncRoot = new();
    private readonly double[] _latestSpectrum = new double[BarCount];
    private readonly CancellationTokenSource _disposeCts = new();
    private Task? _captureTask;
    private bool _disposed;

    public Task<IReadOnlyList<double>> GetSpectrumAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureCaptureStarted();

        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyList<double>>(_latestSpectrum.ToArray());
        }
    }

    private void EnsureCaptureStarted()
    {
        if (_captureTask is not null)
        {
            return;
        }

        lock (_syncRoot)
        {
            _captureTask ??= Task.Run(() => CaptureLoopAsync(_disposeCts.Token));
        }
    }

    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var monitorName = await GetDefaultMonitorNameAsync(cancellationToken);
                if (monitorName is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    continue;
                }

                await CaptureFromMonitorAsync(monitorName, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                SetSpectrum(null);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    private static async Task<string?> GetDefaultMonitorNameAsync(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pactl",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("get-default-sink");

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or global::System.ComponentModel.Win32Exception)
        {
            return null;
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var sinkName = output.Trim();
        return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(sinkName)
            ? $"{sinkName}.monitor"
            : null;
    }

    private async Task CaptureFromMonitorAsync(string monitorName, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "parec",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in new[]
        {
            "--record",
            $"--device={monitorName}",
            "--format=s16le",
            $"--rate={SampleRate}",
            "--channels=1",
            "--raw",
            "--latency-msec=35"
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or global::System.ComponentModel.Win32Exception)
        {
            SetSpectrum(null);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            return;
        }

        try
        {
            var stream = process.StandardOutput.BaseStream;
            var buffer = new byte[SampleCount * BytesPerSample];

            while (!cancellationToken.IsCancellationRequested && !process.HasExited)
            {
                var read = await ReadExactlyOrPartialAsync(stream, buffer, cancellationToken);
                if (read < buffer.Length)
                {
                    break;
                }

                SetSpectrum(AnalyzeSamples(buffer));
            }
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private static async Task<int> ReadExactlyOrPartialAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return offset;
    }

    private static double[] AnalyzeSamples(byte[] buffer)
    {
        var samples = new double[SampleCount];
        var rms = 0d;
        for (var i = 0; i < SampleCount; i++)
        {
            var sample = BitConverter.ToInt16(buffer, i * BytesPerSample) / 32768d;
            var window = 0.5 - (0.5 * Math.Cos((2 * Math.PI * i) / (SampleCount - 1)));
            samples[i] = sample * window;
            rms += sample * sample;
        }

        rms = Math.Sqrt(rms / SampleCount);
        if (rms < SilenceThreshold)
        {
            return new double[BarCount];
        }

        var bars = new double[BarCount];
        for (var band = 0; band < BarCount; band++)
        {
            var startFrequency = FrequencyForBandEdge(band);
            var endFrequency = FrequencyForBandEdge(band + 1);
            var startBin = Math.Max(1, (int)Math.Floor(startFrequency * SampleCount / SampleRate));
            var endBin = Math.Min((SampleCount / 2) - 1, (int)Math.Ceiling(endFrequency * SampleCount / SampleRate));

            var energy = 0d;
            var count = 0;
            for (var bin = startBin; bin <= endBin; bin++)
            {
                var real = 0d;
                var imaginary = 0d;
                var angleStep = (2 * Math.PI * bin) / SampleCount;
                for (var sampleIndex = 0; sampleIndex < SampleCount; sampleIndex++)
                {
                    var angle = angleStep * sampleIndex;
                    real += samples[sampleIndex] * Math.Cos(angle);
                    imaginary -= samples[sampleIndex] * Math.Sin(angle);
                }

                var magnitude = Math.Sqrt((real * real) + (imaginary * imaginary)) / (SampleCount * 0.5);
                energy += magnitude * magnitude;
                count++;
            }

            var bandMagnitude = count == 0 ? 0 : Math.Sqrt(energy / count);
            var decibels = 20 * Math.Log10(bandMagnitude + 0.000001);
            bars[band] = Math.Clamp((decibels + 52) / 46, 0, 1);
        }

        return bars;
    }

    private static double FrequencyForBandEdge(int edge)
    {
        const double minFrequency = 60;
        const double maxFrequency = 7800;
        var ratio = edge / (double)BarCount;
        return minFrequency * Math.Pow(maxFrequency / minFrequency, ratio);
    }

    private void SetSpectrum(double[]? spectrum)
    {
        lock (_syncRoot)
        {
            for (var i = 0; i < BarCount; i++)
            {
                var target = spectrum is null ? 0 : spectrum[i];
                // Fast attack keeps beats immediate; a gentler release removes flicker between samples.
                var response = target > _latestSpectrum[i] ? 0.68 : 0.24;
                _latestSpectrum[i] += (target - _latestSpectrum[i]) * response;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
