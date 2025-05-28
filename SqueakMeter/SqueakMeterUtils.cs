using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using NAudio.Dsp;

namespace FuviiOSC.SqueakMeter;

public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public static class SqueakMeterUtils
{
    public static int ScaleSliderValue(float userValue, int maxValue, int maxInternalValue) => (int)Math.Round(userValue / maxValue * maxInternalValue);

    public static (float bass, float mid, float treble) AnalyzeFrequencies(byte[] buffer, int bytesRecorded, float bassBoost, float midBoost, float trebleBoost, float gain)
    {
        int fftLength = 1024; // must be a power of 2
        int bytesPerSample = 4; // 32-bit float
        int channels = 2;
        int samples = Math.Min(bytesRecorded / bytesPerSample, fftLength * channels);

        // Prepare FFT buffer (average left and right channels)
        Complex[] fftBuffer = new Complex[fftLength];
        for (int i = 0, sample = 0; i < samples && sample < fftLength; i += channels)
        {
            float left = BitConverter.ToSingle(buffer, i * bytesPerSample);
            float right = BitConverter.ToSingle(buffer, (i + 1) * bytesPerSample);
            float sampleValue = (left + right) * 0.5f;
            fftBuffer[sample].X = sampleValue;
            fftBuffer[sample].Y = 0;
            sample++;
        }

        FastFourierTransform.FFT(true, (int)Math.Log(fftLength, 2), fftBuffer);

        // Frequency bins: 0 = DC, N/2 = Nyquist frequency (half sample rate)
        float sampleRate = 48000f;
        float binSize = sampleRate / fftLength;
        float bassRaw = 0, midRaw = 0, trebleRaw = 0;
        int bassBins = 0, midBins = 0, trebleBins = 0;

        for (int i = 0; i < fftLength / 2; i++)
        {
            float freq = i * binSize;
            float magnitude = (float)Math.Sqrt(fftBuffer[i].X * fftBuffer[i].X + fftBuffer[i].Y * fftBuffer[i].Y);

            if (freq < 250) // bass: 0-250Hz
            {
                bassRaw += magnitude;
                bassBins++;
            }
            else if (freq < 4000) // mid: 250-4000Hz
            {
                midRaw += magnitude;
                midBins++;
            }
            else if (freq < 20000) // treble: 4k-20kHz
            {
                trebleRaw += magnitude;
                trebleBins++;
            }
        }

        float bass = bassBins > 0 ? (bassRaw / bassBins) * bassBoost : 0;
        float mid = midBins > 0 ? (midRaw / midBins) * midBoost : 0;
        float treble = trebleBins > 0 ? (trebleRaw / trebleBins) * trebleBoost : 0;

        return (bass, mid, treble);
    }

    // Limit value to the range [0.0, 1.0] with a minimum threshold of 0.01 to avoid flickering
    public static float VRCClamp(float value)
    {
        float clmapedValue = Math.Clamp(value, 0.0f, 1.0f);
        return clmapedValue < 0.01f ? 0.0f : clmapedValue;
    }

    public static float GetSmoothedValue(float firstFloat, float secondFloat, float smooth)
    {
        return VRCClamp(firstFloat * smooth + secondFloat * (1 - smooth));
    }
}
