using System;
using NAudio.Dsp;

namespace FuviiOSC.SqueakMeter;

public static class SqueakMeterUtils
{
    private const int FFT_LENGTH = 1024;
    private const int BYTES_PER_SAMPLE = 4; // 32-bit float
    private const int CHANNELS = 2; // Stereo
    private const float SAMPLE_RATE = 48000f;
    private const float FLICKER_THRESHOLD = 0.01f;

    // Frequency band boundaries (Hz)
    private const float BASS_MAX_FREQ = 250f;
    private const float MID_MAX_FREQ = 4000f;
    private const float TREBLE_MAX_FREQ = 20000f;

    public static int ScaleSliderValue(float userValue, int maxValue, int maxInternalValue)
    {
        return (int)Math.Round(userValue / maxValue * maxInternalValue);
    }

    public static (float Bass, float Mid, float Treble) AnalyzeFrequencies(
        byte[] buffer,
        int bytesRecorded,
        float bassBoost,
        float midBoost,
        float trebleBoost,
        float gain)
    {
        int samples = Math.Min(bytesRecorded / BYTES_PER_SAMPLE, FFT_LENGTH * CHANNELS);

        // Prepare FFT buffer (average left and right channels)
        var fftBuffer = new Complex[FFT_LENGTH];
        PrepareFFTBuffer(buffer, fftBuffer, samples);

        // Perform FFT
        int fftOrder = (int)Math.Log(FFT_LENGTH, 2);
        FastFourierTransform.FFT(true, fftOrder, fftBuffer);

        // Analyze frequency bands
        return CalculateBandMagnitudes(fftBuffer, bassBoost, midBoost, trebleBoost);
    }

    private static void PrepareFFTBuffer(byte[] buffer, Complex[] fftBuffer, int samples)
    {
        int sample = 0;
        for (int i = 0; i < samples && sample < FFT_LENGTH; i += CHANNELS)
        {
            float left = BitConverter.ToSingle(buffer, i * BYTES_PER_SAMPLE);
            float right = BitConverter.ToSingle(buffer, (i + 1) * BYTES_PER_SAMPLE);
            float averagedSample = (left + right) * 0.5f;

            fftBuffer[sample].X = averagedSample;
            fftBuffer[sample].Y = 0;
            sample++;
        }
    }

    private static (float Bass, float Mid, float Treble) CalculateBandMagnitudes(
        Complex[] fftBuffer,
        float bassBoost,
        float midBoost,
        float trebleBoost)
    {
        float binSize = SAMPLE_RATE / FFT_LENGTH;
        float bassSum = 0, midSum = 0, trebleSum = 0;
        int bassBins = 0, midBins = 0, trebleBins = 0;

        // Only process up to Nyquist frequency (half the FFT length)
        int nyquistBin = FFT_LENGTH / 2;

        for (int i = 0; i < nyquistBin; i++)
        {
            float freq = i * binSize;
            float magnitude = MathF.Sqrt(
                fftBuffer[i].X * fftBuffer[i].X +
                fftBuffer[i].Y * fftBuffer[i].Y);

            if (freq < BASS_MAX_FREQ)
            {
                bassSum += magnitude;
                bassBins++;
            }
            else if (freq < MID_MAX_FREQ)
            {
                midSum += magnitude;
                midBins++;
            }
            else if (freq < TREBLE_MAX_FREQ)
            {
                trebleSum += magnitude;
                trebleBins++;
            }
        }

        float bass = bassBins > 0 ? (bassSum / bassBins) * bassBoost : 0;
        float mid = midBins > 0 ? (midSum / midBins) * midBoost : 0;
        float treble = trebleBins > 0 ? (trebleSum / trebleBins) * trebleBoost : 0;

        return (bass, mid, treble);
    }

    public static float VRCClamp(float value)
    {
        float clamped = Math.Clamp(value, 0.0f, 1.0f);
        return clamped < FLICKER_THRESHOLD ? 0.0f : clamped;
    }

    public static float GetSmoothedValue(float previousValue, float newValue, float smoothFactor)
    {
        return VRCClamp(previousValue * smoothFactor + newValue * (1 - smoothFactor));
    }
}
