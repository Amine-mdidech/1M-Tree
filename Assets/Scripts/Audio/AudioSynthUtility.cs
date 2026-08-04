using UnityEngine;

public static class AudioSynthUtility
{
    private const int SampleRate = 44100;

    // Each timbre is a small stack of sine partials (additive synthesis) instead of a raw
    // square/triangle wave - raw square/triangle waves are exactly what reads as "8-bit"; layered
    // sine harmonics instead give a rounder, more produced bell/chime/pad character.
    public enum Timbre
    {
        Soft,
        Bell,
        Warm
    }

    private static readonly float[] SoftRatios = { 1f, 2f };
    private static readonly float[] SoftAmps = { 1f, 0.22f };

    private static readonly float[] BellRatios = { 1f, 2.01f, 3f, 4.2f };
    private static readonly float[] BellAmps = { 1f, 0.5f, 0.24f, 0.12f };

    private static readonly float[] WarmRatios = { 0.5f, 1f, 2f };
    private static readonly float[] WarmAmps = { 0.4f, 1f, 0.25f };

    public struct Note
    {
        public float Frequency;
        public float Duration;

        public Note(float frequency, float duration)
        {
            Frequency = frequency;
            Duration = duration;
        }
    }

    // Extra silent samples appended after the notes/sweep so the echo tail (see ApplySoftEcho) has
    // room to ring out instead of being clipped at the exact end of the last note - without this,
    // short clips (a 45ms tap is shorter than the echo delay itself) would never audibly echo.
    private const float TailPaddingSeconds = 0.16f;

    public static AudioClip CreateSequence(string name, Timbre timbre, float volume, params Note[] notes)
    {
        int totalSamples = 0;
        foreach (Note note in notes)
        {
            totalSamples += Mathf.Max(1, Mathf.RoundToInt(note.Duration * SampleRate));
        }

        totalSamples = Mathf.Max(1, totalSamples);
        int tailPadding = Mathf.RoundToInt(SampleRate * TailPaddingSeconds);
        float[] samples = new float[totalSamples + tailPadding];
        int offset = 0;

        foreach (Note note in notes)
        {
            int noteSamples = Mathf.Max(1, Mathf.RoundToInt(note.Duration * SampleRate));
            WriteTone(samples, offset, noteSamples, note.Frequency, timbre, volume);
            offset += noteSamples;
        }

        ApplySoftEcho(samples, 0.4f);
        return BuildClip(name, samples);
    }

    public static AudioClip CreateSweep(string name, float startFrequency, float endFrequency, float duration, Timbre timbre, float volume)
    {
        int coreSamples = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        int tailPadding = Mathf.RoundToInt(SampleRate * TailPaddingSeconds);
        int totalSamples = coreSamples + tailPadding;
        float[] samples = new float[totalSamples];

        float[] ratios = GetRatios(timbre);
        float[] amps = GetAmps(timbre);
        float ampSum = Sum(amps);
        float[] phases = new float[ratios.Length];

        for (int i = 0; i < coreSamples; i++)
        {
            float progress = coreSamples <= 1 ? 1f : i / (float)(coreSamples - 1);
            float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);

            float sample = 0f;
            for (int p = 0; p < ratios.Length; p++)
            {
                phases[p] += frequency * ratios[p] / SampleRate;
                sample += Mathf.Sin(phases[p] * Mathf.PI * 2f) * amps[p];
            }
            sample /= ampSum;

            float envelope = Envelope(progress, 0.03f, 0.3f);
            samples[i] = sample * volume * envelope;
        }

        ApplySoftEcho(samples, 0.3f);
        return BuildClip(name, samples);
    }

    public static AudioClip CreateAmbientLoop(string name, float duration, float volume)
    {
        int totalSamples = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        float[] samples = new float[totalSamples];

        // Frequencies and LFO rate are chosen so every cycle count over `duration` is an exact
        // integer - each oscillator's phase lands back on zero at the loop point, so looping this
        // clip produces no audible click/seam.
        const float freq1 = 130f;
        const float freq2 = 165f;
        const float freq3 = 196f;
        const float freq4 = 260f;
        const float lfoFrequency = 0.125f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)SampleRate;
            float lfo = 0.6f + 0.4f * Mathf.Sin(t * lfoFrequency * Mathf.PI * 2f);

            float sample =
                Mathf.Sin(t * freq1 * Mathf.PI * 2f) * 0.45f +
                Mathf.Sin(t * freq2 * Mathf.PI * 2f) * 0.28f +
                Mathf.Sin(t * freq3 * Mathf.PI * 2f) * 0.28f +
                Mathf.Sin(t * freq4 * Mathf.PI * 2f) * 0.1f;

            samples[i] = sample * 0.3f * lfo * volume;
        }

        return BuildClip(name, samples);
    }

    private static void WriteTone(float[] samples, int offset, int count, float frequency, Timbre timbre, float volume)
    {
        float[] ratios = GetRatios(timbre);
        float[] amps = GetAmps(timbre);
        float ampSum = Sum(amps);
        float[] phases = new float[ratios.Length];

        for (int i = 0; i < count; i++)
        {
            float progress = count <= 1 ? 1f : i / (float)(count - 1);

            float sample = 0f;
            for (int p = 0; p < ratios.Length; p++)
            {
                phases[p] += frequency * ratios[p] / SampleRate;
                sample += Mathf.Sin(phases[p] * Mathf.PI * 2f) * amps[p];
            }
            sample /= ampSum;

            float envelope = Envelope(progress, 0.02f, 0.22f);
            samples[offset + i] = sample * volume * envelope;
        }
    }

    private static float[] GetRatios(Timbre timbre)
    {
        switch (timbre)
        {
            case Timbre.Bell:
                return BellRatios;
            case Timbre.Warm:
                return WarmRatios;
            default:
                return SoftRatios;
        }
    }

    private static float[] GetAmps(Timbre timbre)
    {
        switch (timbre)
        {
            case Timbre.Bell:
                return BellAmps;
            case Timbre.Warm:
                return WarmAmps;
            default:
                return SoftAmps;
        }
    }

    private static float Sum(float[] values)
    {
        float total = 0f;
        foreach (float value in values)
        {
            total += value;
        }

        return total;
    }

    // A cheap feedback-free echo: mix in a couple of quiet, delayed copies of the dry signal.
    // Turns a flat synthesized tone into something with a sense of space instead of sounding dry
    // and clinical.
    private static void ApplySoftEcho(float[] samples, float wetAmount)
    {
        int delay1 = Mathf.RoundToInt(SampleRate * 0.045f);
        int delay2 = Mathf.RoundToInt(SampleRate * 0.095f);

        float[] dry = (float[])samples.Clone();

        for (int i = 0; i < samples.Length; i++)
        {
            float echo = 0f;
            if (i - delay1 >= 0)
            {
                echo += dry[i - delay1] * 0.35f;
            }

            if (i - delay2 >= 0)
            {
                echo += dry[i - delay2] * 0.18f;
            }

            samples[i] = dry[i] + echo * wetAmount;
        }
    }

    private static float Envelope(float progress, float attack, float releaseStart)
    {
        if (progress < attack)
        {
            return progress / attack;
        }

        if (progress > releaseStart)
        {
            float releaseProgress = (progress - releaseStart) / (1f - releaseStart);
            // Exponential-ish decay (via squaring) instead of a linear fade reads as a natural
            // "ring out" rather than an abrupt cutoff.
            float linear = Mathf.Clamp01(1f - releaseProgress);
            return linear * linear;
        }

        return 1f;
    }

    private static AudioClip BuildClip(string name, float[] samples)
    {
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
