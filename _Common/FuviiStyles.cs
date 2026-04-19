using System.Windows.Media;

namespace FuviiOSC.Common;

public static class FuviiStyles
{
    // Primary (Purple)
    public static readonly Color Purple       = Color.FromRgb(0x69, 0x32, 0xDD);
    public static readonly Color PurpleLight  = Color.FromRgb(0x9E, 0x65, 0xFF);
    // Secondary (Gold)
    public static readonly Color Gold         = Color.FromRgb(0xF9, 0xC0, 0x3C);
    // Accent colours
    public static readonly Color Cyan         = Color.FromRgb(0x40, 0xC8, 0xE0);
    public static readonly Color Green        = Color.FromRgb(0x40, 0xC0, 0x70);
    public static readonly Color Orange       = Color.FromRgb(0xE6, 0x64, 0x22);
    // Semantic colours
    public static readonly Color DangerRed    = Color.FromRgb(0xD3, 0x2F, 0x2F);
    public static readonly Color SuccessGreen = Color.FromRgb(0x38, 0x8E, 0x3C);
    public static readonly Color EditBlue     = Color.FromRgb(0x19, 0x76, 0xD2);
    // Neutrals
    public static readonly Color Inactive     = Color.FromRgb(0x64, 0x64, 0x64);
    public static readonly Color DarkBg       = Color.FromRgb(0x32, 0x32, 0x32);
    // Semi-transparent white helpers
    public static readonly Color WhiteSubtle     = Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF);
    public static readonly Color WhiteDim        = Color.FromArgb(0x64, 0xFF, 0xFF, 0xFF);
    public static readonly Color WhiteSoft       = Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF);
    public static readonly Color WhiteGridLine   = Color.FromArgb(0x32, 0xFF, 0xFF, 0xFF);
    // Pre-frozen brushes (safe for cross-thread use)
    public static readonly SolidColorBrush PurpleBrush       = Freeze(new SolidColorBrush(Purple));
    public static readonly SolidColorBrush PurpleFillBrush   = Freeze(new SolidColorBrush(Color.FromArgb(0x66, 0x69, 0x32, 0xDD)));
    public static readonly SolidColorBrush GoldBrush         = Freeze(new SolidColorBrush(Gold));
    public static readonly SolidColorBrush CyanBrush         = Freeze(new SolidColorBrush(Cyan));
    public static readonly SolidColorBrush GreenBrush        = Freeze(new SolidColorBrush(Green));
    public static readonly SolidColorBrush OrangeBrush       = Freeze(new SolidColorBrush(Orange));
    public static readonly SolidColorBrush InactiveBrush     = Freeze(new SolidColorBrush(Inactive));
    public static readonly SolidColorBrush DarkBgBrush       = Freeze(new SolidColorBrush(DarkBg));
    public static readonly SolidColorBrush WhiteSubtleBrush  = Freeze(new SolidColorBrush(WhiteSubtle));
    public static readonly SolidColorBrush WhiteDimBrush     = Freeze(new SolidColorBrush(WhiteDim));
    public static readonly SolidColorBrush WhiteSoftBrush    = Freeze(new SolidColorBrush(WhiteSoft));
    public static readonly SolidColorBrush GridLineBrush     = Freeze(new SolidColorBrush(WhiteGridLine));
    // Half-opacity accent brushes (peak markers etc.)
    public static readonly SolidColorBrush PurplePeakBrush   = Freeze(new SolidColorBrush(Color.FromArgb(0x80, 0x69, 0x32, 0xDD)));
    public static readonly SolidColorBrush GoldPeakBrush     = Freeze(new SolidColorBrush(Color.FromArgb(0x80, 0xF9, 0xC0, 0x3C)));
    public static readonly SolidColorBrush CyanPeakBrush     = Freeze(new SolidColorBrush(Color.FromArgb(0x80, 0x40, 0xC8, 0xE0)));
    public static readonly SolidColorBrush GreenPeakBrush    = Freeze(new SolidColorBrush(Color.FromArgb(0x80, 0x40, 0xC0, 0x70)));

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
