using System.IO;
using System.Media;

namespace DnDMapHelper.Helpers;

public static class ScrollSoundHelper
{
    private const string CacheFileName = "DnDMapHelper-scroll-open-v2.wav";

    private static string? _cachedWavPath;

    public static void PlayOpen()
    {
        try
        {
            var player = new SoundPlayer(GetWavPath());
            player.Play();
        }
        catch
        {
            // Звук необязателен — не мешаем показу свитка.
        }
    }

    private static string GetWavPath()
    {
        if (_cachedWavPath is not null && File.Exists(_cachedWavPath))
            return _cachedWavPath;

        _cachedWavPath = Path.Combine(Path.GetTempPath(), CacheFileName);
        File.WriteAllBytes(_cachedWavPath, BuildScrollOpenWav());
        return _cachedWavPath;
    }

    /// <summary>Шуршание пергамента: несколько «складок» + непрерывное разворачивание.</summary>
    private static byte[] BuildScrollOpenWav()
    {
        const int sampleRate = 44100;
        const double durationSeconds = 0.62;
        var sampleCount = (int)(sampleRate * durationSeconds);
        var buffer = new double[sampleCount];
        var random = new Random(137);

        AddUnrollScrape(buffer, sampleRate, random);

        (double start, double length, double intensity, double crisp)[] crinkles =
        [
            (0.000, 0.070, 0.95, 0.92),
            (0.035, 0.090, 0.88, 0.95),
            (0.085, 0.110, 0.78, 0.88),
            (0.145, 0.085, 0.62, 0.90),
            (0.205, 0.100, 0.58, 0.85),
            (0.275, 0.095, 0.48, 0.82),
            (0.345, 0.080, 0.38, 0.78),
            (0.410, 0.075, 0.30, 0.74),
            (0.470, 0.065, 0.22, 0.70),
        ];

        foreach (var (start, length, intensity, crisp) in crinkles)
            AddCrinkle(buffer, sampleRate, random, start, length, intensity, crisp);

        ApplyMasterEnvelope(buffer, sampleRate);
        Normalize(buffer, 0.88);

        return WrapPcmAsWav(ConvertToPcm16(buffer), sampleRate);
    }

    private static void AddUnrollScrape(double[] buffer, int sampleRate, Random random)
    {
        var lowPass = 0.0;
        var count = buffer.Length;

        for (var i = 0; i < count; i++)
        {
            var t = (double)i / count;
            if (t > 0.58)
                continue;

            var envelope = (1 - Math.Exp(-t * 28)) * Math.Pow(1 - t / 0.58, 0.75);
            var white = random.NextDouble() * 2 - 1;
            lowPass = lowPass * 0.90 + white * 0.10;
            var high = white - lowPass;

            buffer[i] += (lowPass * 0.22 + high * 0.48) * envelope * 0.42;
        }
    }

    private static void AddCrinkle(
        double[] buffer,
        int sampleRate,
        Random random,
        double startSeconds,
        double lengthSeconds,
        double intensity,
        double crispness)
    {
        var start = (int)(startSeconds * sampleRate);
        var length = Math.Max(8, (int)(lengthSeconds * sampleRate));
        var end = Math.Min(buffer.Length, start + length);
        if (start >= buffer.Length)
            return;

        var lowPass = 0.0;
        for (var i = start; i < end; i++)
        {
            var localT = (double)(i - start) / length;
            var attack = Math.Min(1, localT * 18);
            var decay = Math.Exp(-localT * 2.8);
            var envelope = attack * decay * Math.Sin(localT * Math.PI);

            var white = random.NextDouble() * 2 - 1;
            lowPass = lowPass * 0.82 + white * 0.18;
            var high = white - lowPass;

            buffer[i] += (lowPass * (1 - crispness) * 0.35 + high * crispness * 0.65) * envelope * intensity;
        }
    }

    private static void ApplyMasterEnvelope(double[] buffer, int sampleRate)
    {
        var fadeInSamples = (int)(0.012 * sampleRate);
        var fadeOutSamples = (int)(0.14 * sampleRate);
        var count = buffer.Length;

        for (var i = 0; i < count; i++)
        {
            var fadeIn = i < fadeInSamples ? (double)i / fadeInSamples : 1;
            var fadeOut = i > count - fadeOutSamples
                ? (double)(count - i) / fadeOutSamples
                : 1;
            buffer[i] *= fadeIn * fadeOut * fadeOut;
        }
    }

    private static void Normalize(double[] buffer, double peakTarget)
    {
        var peak = 0.0;
        foreach (var sample in buffer)
            peak = Math.Max(peak, Math.Abs(sample));

        if (peak <= 0.0001)
            return;

        var scale = peakTarget / peak;
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] *= scale;
    }

    private static byte[] ConvertToPcm16(IReadOnlyList<double> samples)
    {
        var pcm = new byte[samples.Count * 2];
        for (var i = 0; i < samples.Count; i++)
        {
            var value = (short)Math.Clamp(samples[i] * short.MaxValue, short.MinValue, short.MaxValue);
            pcm[i * 2] = (byte)(value & 0xFF);
            pcm[i * 2 + 1] = (byte)(value >> 8);
        }

        return pcm;
    }

    private static byte[] WrapPcmAsWav(byte[] pcmData, int sampleRate)
    {
        using var stream = new MemoryStream(44 + pcmData.Length);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + pcmData.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(pcmData.Length);
        writer.Write(pcmData);

        return stream.ToArray();
    }
}
