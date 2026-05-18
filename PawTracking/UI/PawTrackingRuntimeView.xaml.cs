using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using FuviiOSC.Common;
using VRCOSC.App.OpenVR;
using VRCOSC.App.OpenVR.Device;

namespace FuviiOSC.PawTracking.UI;

public partial class PawTrackingRuntimeView
{
    private static readonly Dictionary<VRCGesture, Geometry> GestureIcons = CreateGestureIcons();

    private const double TrackerIconSize = 22;

    // Tracker body-diagram groups listed top-to-bottom
    private record TrackerGroupDef(string DisplayName, string? LeftRole, string? RightRole, bool AlwaysVisible)
    {
        public bool IsPaired => LeftRole is not null && RightRole is not null;
        public string? SingleRole => IsPaired ? null : (LeftRole ?? RightRole);
    }

    private static readonly TrackerGroupDef[] TrackerGroupDefs =
    [
        // Always-visible trackers
        new("Chest",    "Chest",         null,              true),
        new("Waist",    "Waist",         null,              true),
        new("Foot",     "LeftFoot",      "RightFoot",       true),
        // Connected trackers (shown only when at least one is connected)
        new("Shoulder", "LeftShoulder",  "RightShoulder",   false),
        new("Elbow",    "LeftElbow",     "RightElbow",      false),
        new("Wrist",    "LeftWrist",     "RightWrist",      false),
        new("Knee",     "LeftKnee",      "RightKnee",       false),
        new("Ankle",    "LeftAnkle",     "RightAnkle",      false),
        new("Camera",   "Camera",        null,              false),
    ];

    private readonly Dictionary<string, Border> _trackerIcons = new();
    private readonly Dictionary<string, Rectangle> _trackerBatteryFills = new();
    private readonly Dictionary<string, Grid> _trackerRows = new();

    private readonly PawTrackingModule _module;
    private readonly DispatcherTimer _timer;

    public PawTrackingRuntimeView(PawTrackingModule module)
    {
        _module = module;
        DataContext = this;
        InitializeComponent();
        BuildTrackerPanel();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    #region Gesture Icons

    private static Dictionary<VRCGesture, Geometry> CreateGestureIcons()
    {
        SvgPathDef[] svgPaths =
        [
            new(VRCGesture.Fist, 0.05,
                "M1710 1752 c-32 -24 -98 -31 -322 -31 -255 -1 -288 -5 -335 -42 -73 -58 -75 -183 -4 -230 40 -27 46 -43 37 -101 -9 -53 -2 -80 30 -114 30 -32 37 -53 23 -74 -32 -52 -21 -101 38 -160 38 -38 51 -66 40 -84 -93 -147 25 -216 368 -216 524 0 776 178 782 550 4 309 -454 660 -657 502z m311 -94 c400 -172 353 -784 -67 -868 -35 -7 -79 -17 -98 -22 -28 -8 -32 1 -23 56 6 41 -1 81 -20 106 -26 36 -24 45 26 102 44 50 57 89 67 197 7 75 24 148 37 164 18 22 18 34 -1 53 -39 38 -77 -32 -92 -172 -21 -202 -55 -245 -149 -194 -60 31 -41 538 22 614 33 39 159 24 298 -36z m-388 -43 c-7 -25 -13 -65 -13 -90 0 -53 -485 -72 -536 -21 -35 35 -29 111 11 134 81 47 551 27 538 -23z m-21 -227 c-7 -17 -12 -58 -12 -90 l0 -58 -191 0 c-246 0 -341 60 -245 156 40 40 464 33 448 -8z m-12 -257 c0 -28 22 -66 55 -92 l55 -45 -80 13 c-44 7 -154 13 -244 13 -182 0 -233 33 -186 120 36 68 400 60 400 -9z m159 -230 c58 -107 1 -141 -239 -141 -235 0 -330 62 -236 156 45 45 450 32 475 -15z"),

            new(VRCGesture.HandOpen, 0.1,
                "M840 940 c-32 -32 -26 -79 20 -143 22 -30 40 -58 40 -61 0 -3 -99 -6 -219 -6 -214 0 -221 -1 -246 -22 -27 -24 -33 -48 -21 -85 5 -17 3 -23 -8 -23 -21 0 -56 -48 -56 -77 0 -29 41 -73 69 -73 17 0 18 -5 14 -39 -5 -33 -1 -44 20 -65 14 -14 35 -26 47 -26 17 0 19 -4 14 -25 -10 -40 4 -73 37 -90 26 -14 65 -16 252 -13 207 3 225 5 272 26 99 46 155 125 163 230 5 81 -7 114 -75 198 -27 33 -84 112 -127 175 -43 62 -86 120 -94 127 -24 19 -79 15 -102 -8z m183 -155 c53 -76 108 -151 121 -165 73 -77 87 -165 40 -258 -31 -63 -79 -104 -146 -126 -73 -23 -459 -24 -482 0 -19 19 -21 60 -3 71 6 4 87 10 180 13 243 7 220 24 -37 28 -134 1 -214 6 -223 13 -18 15 -16 54 3 73 13 14 47 16 216 16 155 0 199 3 196 13 -4 9 -64 13 -247 15 -242 2 -242 2 -252 26 -8 17 -8 29 0 45 11 20 19 21 248 23 178 2 237 6 241 16 3 9 -42 12 -202 12 -193 0 -207 1 -226 20 -24 24 -25 32 -2 59 16 20 26 21 265 24 248 2 248 2 193 82 -31 44 -56 89 -56 100 0 29 20 46 50 43 20 -2 46 -33 123 -143z"),

            new(VRCGesture.FingerPoint, 0.1,
                "M881 764 c-20 -15 -56 -17 -293 -18 -270 -1 -270 -1 -289 -25 -25 -30 -24 -76 1 -101 18 -18 33 -20 148 -20 128 0 128 0 124 -39 -3 -30 1 -43 18 -59 14 -13 18 -22 11 -27 -19 -11 -12 -65 12 -89 18 -18 22 -31 19 -63 -3 -32 1 -46 17 -62 19 -19 32 -21 170 -21 188 0 234 13 307 85 139 138 100 339 -82 424 -62 30 -134 36 -163 15z m157 -46 c54 -26 110 -80 128 -124 48 -115 -27 -265 -155 -309 -66 -22 -83 -20 -72 9 7 16 5 32 -4 50 -12 22 -12 31 -2 47 6 10 15 19 18 19 11 0 27 53 33 110 3 30 8 65 12 78 4 14 2 22 -5 22 -19 0 -41 -64 -41 -116 0 -74 -45 -108 -84 -65 -16 17 -16 30 -5 147 14 155 22 169 88 160 25 -3 65 -16 89 -28z m-198 -43 c0 -45 0 -45 -254 -45 -212 0 -256 2 -266 15 -16 19 -5 62 18 68 9 3 126 5 260 6 242 1 242 1 242 -44z m-10 -120 c0 -46 0 -46 -106 -43 -95 3 -108 5 -120 23 -12 18 -11 24 2 43 14 20 23 22 120 22 104 0 104 0 104 -45z m-2 -109 c7 -18 22 -38 35 -44 17 -8 -3 -11 -91 -11 -122 -1 -152 7 -152 42 0 39 18 47 110 47 86 0 86 0 98 -34z m76 -108 c13 -19 14 -25 2 -43 -12 -18 -25 -20 -116 -23 -109 -3 -130 4 -130 42 0 39 18 46 126 46 95 0 104 -2 118 -22z"),

            new(VRCGesture.Victory, 0.1,
                "M345 820 c-24 -27 -23 -76 2 -101 15 -16 57 -26 194 -47 l174 -27 -173 -5 c-168 -5 -174 -6 -193 -29 -25 -30 -24 -62 2 -95 l21 -27 134 3 c116 3 133 1 128 -12 -14 -30 -5 -65 20 -87 23 -19 25 -26 16 -44 -16 -28 -5 -65 25 -85 20 -13 50 -15 187 -12 145 3 168 6 204 25 64 34 97 66 129 125 38 69 41 161 8 224 -56 106 -223 191 -298 152 -23 -13 -53 -10 -258 24 -271 44 -297 45 -322 18z m301 -45 c121 -19 222 -35 227 -35 10 0 9 -62 -2 -78 -6 -10 -52 -6 -232 23 -123 20 -234 38 -246 40 -54 12 -49 85 6 85 16 0 127 -16 247 -35z m427 -47 c58 -25 123 -96 138 -152 34 -121 -67 -271 -197 -291 l-40 -7 4 34 c2 18 -3 41 -11 52 -13 17 -11 22 12 49 19 21 29 47 37 100 6 40 14 80 18 90 6 12 4 17 -8 17 -20 0 -31 -28 -41 -105 -8 -68 -18 -85 -50 -85 -41 0 -48 21 -40 127 9 127 20 183 36 194 19 12 83 1 142 -23z m-207 -150 c-4 -23 -8 -43 -9 -44 -1 -1 -107 -6 -234 -10 -243 -7 -263 -4 -263 35 1 48 17 50 424 59 l89 2 -7 -42z m-6 -112 c0 -15 11 -33 27 -46 l27 -21 -119 3 c-106 3 -119 5 -131 23 -12 18 -11 24 2 43 14 20 24 22 105 22 88 0 89 0 89 -24z m71 -106 c21 -12 25 -50 7 -68 -8 -8 -49 -12 -120 -12 -106 0 -109 1 -120 25 -9 21 -8 28 7 45 16 17 31 20 112 20 52 0 103 -5 114 -10z"),

            new(VRCGesture.RockNRoll, 0.05,
                "M1917 1722 c-51 -40 -76 -42 -594 -42 -602 0 -603 0 -603 -144 0 -115 51 -136 335 -136 l255 0 -7 -89 c-5 -72 1 -93 34 -114 32 -20 36 -33 20 -63 -12 -21 -17 -65 -12 -96 l8 -58 -198 0 c-282 0 -408 -133 -252 -265 59 -50 1157 -53 1302 -3 289 98 450 436 335 704 -101 234 -479 420 -623 306z m243 -56 c390 -126 480 -578 164 -820 -130 -98 -185 -106 -795 -106 -593 0 -609 2 -609 93 0 79 53 87 565 87 361 0 495 6 517 25 77 66 103 121 120 249 9 75 23 161 30 191 19 81 -28 70 -63 -14 -16 -39 -29 -112 -29 -164 0 -146 -90 -216 -167 -130 -31 34 -32 61 -11 285 32 350 56 376 278 304z m-320 -128 l0 -79 -513 6 c-463 4 -515 8 -535 40 -59 95 -16 104 520 115 577 12 528 19 528 -82z m-20 -228 l0 -90 -205 0 c-214 1 -239 7 -261 65 -30 81 49 115 266 115 l200 0 0 -90z m-1 -228 c13 -31 43 -67 67 -78 34 -16 -13 -22 -197 -23 -246 -1 -289 11 -289 79 0 104 377 124 419 22z"),

            new(VRCGesture.HandGun, 0.05,
                "M1936 1839 c-65 -65 -67 -99 -24 -394 l12 -85 -567 0 c-312 0 -577 -7 -589 -15 -40 -27 -72 -108 -60 -155 25 -100 56 -110 332 -110 l256 0 -10 -75 c-8 -58 -1 -83 30 -111 34 -31 36 -45 18 -94 -27 -70 7 -159 66 -174 31 -8 35 -18 20 -47 -93 -172 6 -224 413 -216 382 8 484 41 609 199 199 252 173 467 -94 776 -153 178 -151 172 -218 497 -18 89 -107 91 -194 4z m133 -24 c4 -14 22 -93 40 -177 31 -144 41 -163 192 -340 185 -218 226 -315 197 -469 -38 -201 -199 -361 -404 -400 l-74 -13 13 62 c8 40 1 78 -20 110 -28 42 -28 51 -2 73 42 35 36 152 -10 198 -23 23 -34 54 -28 78 10 37 16 35 80 -28 37 -38 86 -69 108 -69 55 0 49 29 -16 71 -121 79 -150 149 -167 409 -8 116 -21 261 -29 323 -16 132 86 280 120 172z m-146 -603 c5 -69 0 -104 -13 -95 -11 7 -267 18 -568 23 -493 9 -550 13 -570 45 -60 95 -18 104 550 116 290 5 543 12 561 15 27 3 35 -17 40 -104z m-42 -153 c44 -23 52 -98 15 -135 -33 -33 -499 -33 -532 0 -35 35 -29 111 11 134 46 27 457 28 506 1z m75 -243 c13 -13 24 -43 24 -66 0 -79 -36 -90 -293 -90 -250 0 -307 17 -307 90 0 85 499 143 576 66z m-6 -256 c88 -97 19 -140 -226 -140 -229 0 -264 12 -264 93 0 95 411 134 490 47z"),

            new(VRCGesture.ThumbsUp, 0.1,
                "M874 1010 c-59 -24 -77 -91 -49 -184 26 -86 36 -81 -146 -78 -156 4 -162 3 -185 -19 -13 -13 -24 -34 -24 -49 0 -30 25 -70 45 -70 11 0 11 -6 2 -30 -10 -25 -8 -34 11 -59 16 -22 20 -36 14 -52 -12 -33 -3 -68 23 -85 20 -13 22 -18 12 -44 -10 -25 -8 -34 9 -57 l21 -28 167 -3 c151 -2 171 -1 216 19 56 24 121 89 146 146 23 54 23 158 0 203 -10 19 -46 67 -80 105 -87 99 -84 94 -101 176 -8 41 -17 84 -20 97 -6 23 -25 27 -61 12z m40 -57 c23 -132 27 -142 104 -231 78 -90 111 -149 112 -193 0 -46 -29 -123 -62 -163 -34 -41 -116 -86 -159 -86 -22 0 -25 3 -21 29 2 16 -1 38 -7 50 -9 17 -9 26 4 43 21 31 19 52 -8 85 -13 15 -21 36 -19 46 4 18 22 24 22 7 0 -10 69 -60 82 -60 20 0 5 31 -23 45 -21 11 -39 34 -56 72 -22 49 -25 64 -19 123 4 48 2 80 -9 110 -24 67 -20 115 11 139 36 28 41 27 48 -16z m-100 -249 c20 -20 20 -33 -1 -56 -14 -16 -33 -18 -145 -18 -138 0 -168 8 -168 43 0 42 16 47 161 47 112 0 141 -3 153 -16z m16 -124 c8 -14 8 -26 0 -40 -10 -18 -21 -20 -138 -20 -130 0 -152 6 -152 40 0 34 22 40 152 40 117 0 128 -2 138 -20z m25 -110 c15 -17 16 -24 7 -45 -11 -25 -13 -25 -139 -25 -71 0 -133 4 -139 8 -22 15 -24 42 -4 62 18 18 34 20 138 20 106 0 121 -2 137 -20z m-5 -125 c7 -8 10 -25 6 -40 -6 -25 -7 -25 -119 -25 -115 0 -137 6 -137 40 0 33 22 40 131 40 82 0 109 -3 119 -15z"),
        ];

        const double viewBoxH = 128.0;
        List<SvgIconEntry> entries = new();

        foreach (SvgPathDef def in svgPaths)
        {
            Geometry tmp = Geometry.Parse(def.Path).Clone();
            tmp.Transform = new MatrixTransform(def.Scale, 0, 0, -def.Scale, 0, viewBoxH);
            entries.Add(new SvgIconEntry(def.Gesture, def.Path, def.Scale, tmp.Bounds));
        }

        // Find alignment anchors: right wrist edge, bottom palm base
        double maxRight = 0, maxBottom = 0;
        foreach (SvgIconEntry e in entries)
        {
            maxRight = Math.Max(maxRight, e.Bounds.Right);
            maxBottom = Math.Max(maxBottom, e.Bounds.Bottom);
        }

        double finalMinX = double.MaxValue, finalMinY = double.MaxValue;
        double finalMaxX = 0, finalMaxY = 0;
        List<ShiftedSvgIcon> shifted = new();
        foreach ((VRCGesture gesture, string pathData, double s, Rect bounds) in entries)
        {
            double dx = maxRight - bounds.Right;   // wrist stays at right edge
            double dy = maxBottom - bounds.Bottom;  // palm base stays at bottom
            shifted.Add(new ShiftedSvgIcon(gesture, pathData, s, dx, dy));
            finalMinX = Math.Min(finalMinX, bounds.Left + dx);
            finalMinY = Math.Min(finalMinY, bounds.Top + dy);
            finalMaxX = Math.Max(finalMaxX, bounds.Right + dx);
            finalMaxY = Math.Max(finalMaxY, bounds.Bottom + dy);
        }

        // Padding anchors at union corners to keep all icons in the same bounding box
        Point padTL = new(finalMinX, finalMinY);
        Point padBR = new(finalMaxX, finalMaxY);

        Dictionary<VRCGesture, Geometry> icons = new();
        foreach ((VRCGesture gesture, string pathData, double s, double dx, double dy) in shifted)
        {
            TransformGroup transform = new();
            transform.Children.Add(new MatrixTransform(s, 0, 0, -s, 0, viewBoxH));
            transform.Children.Add(new TranslateTransform(dx, dy));

            Geometry geo = Geometry.Parse(pathData).Clone();
            geo.Transform = transform;

            GeometryGroup group = new() { FillRule = FillRule.Nonzero };
            group.Children.Add(geo);
            group.Children.Add(new EllipseGeometry(padTL, 0.01, 0.01));
            group.Children.Add(new EllipseGeometry(padBR, 0.01, 0.01));
            group.Freeze();
            icons[gesture] = group;
        }

        if (icons.TryGetValue(VRCGesture.HandOpen, out Geometry? openGeo))
            icons[VRCGesture.Neutral] = openGeo;

        return icons;
    }

    #endregion
    #region Tracker Panel

    // Builds the center tracker body-diagram panel. Every row uses a 3-column Grid:
    // [Left icon | Label | Right icon] so single-tracker rows align with paired rows
    private void BuildTrackerPanel()
    {
        Brush fgBrush = (Brush)FindResource("CForeground2");

        foreach (TrackerGroupDef group in TrackerGroupDefs)
        {
            Grid row = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2),
                Visibility = group.AlwaysVisible ? Visibility.Visible : Visibility.Collapsed
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });

            TextBlock label = new()
            {
                Text = group.DisplayName,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = fgBrush
            };
            Grid.SetColumn(label, 1);
            row.Children.Add(label);

            if (group.IsPaired)
            {
                (Border leftIcon, Rectangle leftFill) = CreateTrackerIcon("L");
                Grid.SetColumn(leftIcon, 0);
                row.Children.Add(leftIcon);
                _trackerIcons[group.LeftRole!] = leftIcon;
                _trackerBatteryFills[group.LeftRole!] = leftFill;

                (Border rightIcon, Rectangle rightFill) = CreateTrackerIcon("R");
                Grid.SetColumn(rightIcon, 2);
                row.Children.Add(rightIcon);
                _trackerIcons[group.RightRole!] = rightIcon;
                _trackerBatteryFills[group.RightRole!] = rightFill;
            }
            else
            {
                (Border icon, Rectangle fill) = CreateTrackerIcon("x");
                Grid.SetColumn(icon, 0);
                row.Children.Add(icon);
                _trackerIcons[group.SingleRole!] = icon;
                _trackerBatteryFills[group.SingleRole!] = fill;
            }

            _trackerRows[group.DisplayName] = row;
            TrackerPanel.Children.Add(row);
        }
    }

    private static (Border border, Rectangle batteryFill) CreateTrackerIcon(string label)
    {
        const double borderT = 1.5;
        double innerSize = TrackerIconSize - 2 * borderT;

        Rectangle batteryRect = new()
        {
            Fill = Brushes.Transparent,
            Height = 0,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        TextBlock textBlock = new()
        {
            Text = label,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -1.5, 0, 0),
            Foreground = FuviiStyles.InactiveBrush
        };
        Grid innerGrid = new()
        {
            Clip = new RectangleGeometry(
                new Rect(0, 0, innerSize, innerSize), innerSize / 2, innerSize / 2)
        };
        innerGrid.Children.Add(batteryRect);
        innerGrid.Children.Add(textBlock);

        Border border = new()
        {
            Width = TrackerIconSize, Height = TrackerIconSize,
            CornerRadius = new CornerRadius(TrackerIconSize / 2),
            Background = FuviiStyles.DarkBgBrush,
            BorderBrush = FuviiStyles.InactiveBrush,
            BorderThickness = new Thickness(borderT),
            Child = innerGrid
        };

        return (border, batteryRect);
    }

    #endregion

    private void OnTick(object? sender, EventArgs e)
    {
        try
        {
            OpenVRManager manager = _module.GetOpenVRManager();
            Controller? lc = manager.GetLeftController();
            Controller? rc = manager.GetRightController();

            UpdateHand(lc, true);
            UpdateHand(rc, false);
            UpdateTrackers();

            double controllerOpacity = _module.ControllerInputsEnabled ? 1.0 : 0.3;
            LeftHandPanel.Opacity = controllerOpacity;
            RightHandPanel.Opacity = controllerOpacity;
            CenterTrackerSection.Opacity = _module.TrackerButtonsEnabled ? 1.0 : 0.3;
        }
        catch (Exception ex)
        {
            _module.LogDebug($"RuntimeView tick failed: {ex.Message}");
        }
    }

    private void UpdateHand(Controller? controller, bool isLeft)
    {
        if (controller is null)
        {
            if (isLeft)
            {
                LeftGestureText.Text = "N/A"; LeftWeightText.Text = "-"; LeftClosestText.Text = "-";
                LeftGestureIcon.Data = null;
                LeftGestureIcon.Fill = FuviiStyles.InactiveBrush;
            }
            else
            {
                RightGestureText.Text = "N/A"; RightWeightText.Text = "-"; RightClosestText.Text = "-";
                RightGestureIcon.Data = null;
                RightGestureIcon.Fill = FuviiStyles.InactiveBrush;
            }
            return;
        }

        InputState input = controller.Input;
        Skeleton fingers = input.Skeleton;

        if (isLeft)
        {
            GestureResult gesture = _module.LastLeftGesture;
            LeftGestureText.Text = gesture.Gesture.ToString();
            LeftWeightText.Text = gesture.Weight.ToString("F2");
            LeftClosestText.Text = $"{gesture.ClosestGesture} ({gesture.ClosestWeight:F2})";

            if (GestureIcons.TryGetValue(gesture.Gesture, out Geometry? geo))
                LeftGestureIcon.Data = geo;

            LeftGestureIcon.Fill = gesture.Gesture != VRCGesture.Neutral && gesture.Weight >= 1.0f
                ? FuviiStyles.GoldBrush
                : gesture.Gesture != VRCGesture.Neutral ? FuviiStyles.PurpleBrush : FuviiStyles.InactiveBrush;

            SetBar(LeftIndexBar, LeftIndexText, fingers.Index);
            SetBar(LeftMiddleBar, LeftMiddleText, fingers.Middle);
            SetBar(LeftRingBar, LeftRingText, fingers.Ring);
            SetBar(LeftPinkyBar, LeftPinkyText, fingers.Pinky);

            SetButton(LeftABtn, LeftALbl, input.Primary.Touch, input.Primary.Click);
            SetButton(LeftBBtn, LeftBLbl, input.Secondary.Touch, input.Secondary.Click);

            SetGaugeVisual(LeftTriggerBorder, LeftTriggerFill, LeftTriggerLbl, LeftTriggerVal,
                input.Trigger.Pull, 45.0, input.Trigger.Touch, input.Trigger.Click);

            LeftGripPullVal.Text = input.Grip.Pull.ToString("F2");
            SetGaugeVisual(LeftGripBorder, LeftGripFill, LeftGripLbl, LeftGripVal,
                input.Grip.Pull, 39.0, false, input.Grip.Click);
            LeftGripVal.Text = _module.LeftGripForce.ToString("F2");
            SetGripForceOverlay(LeftGripForceFill, input.Grip.Pull, _module.LeftGripForce, 39.0);

            SetStick(LeftStickBorder, LeftStickDot, LeftStickCoord,
                input.Stick.Position.X, input.Stick.Position.Y, input.Stick.Touch, input.Stick.Click);

            SetPad(LeftPadBorder, LeftPadDot, LeftPadCoord,
                input.Pad.Position.X, input.Pad.Position.Y, input.Pad.Touch, input.Pad.Click);
        }
        else
        {
            GestureResult gesture = _module.LastRightGesture;
            RightGestureText.Text = gesture.Gesture.ToString();
            RightWeightText.Text = gesture.Weight.ToString("F2");
            RightClosestText.Text = $"{gesture.ClosestGesture} ({gesture.ClosestWeight:F2})";

            if (GestureIcons.TryGetValue(gesture.Gesture, out Geometry? geo))
                RightGestureIcon.Data = geo;

            RightGestureIcon.Fill = gesture.Gesture != VRCGesture.Neutral && gesture.Weight >= 1.0f
                ? FuviiStyles.GoldBrush
                : gesture.Gesture != VRCGesture.Neutral ? FuviiStyles.PurpleBrush : FuviiStyles.InactiveBrush;

            SetBar(RightIndexBar, RightIndexText, fingers.Index);
            SetBar(RightMiddleBar, RightMiddleText, fingers.Middle);
            SetBar(RightRingBar, RightRingText, fingers.Ring);
            SetBar(RightPinkyBar, RightPinkyText, fingers.Pinky);

            SetButton(RightABtn, RightALbl, input.Primary.Touch, input.Primary.Click);
            SetButton(RightBBtn, RightBLbl, input.Secondary.Touch, input.Secondary.Click);

            SetGaugeVisual(RightTriggerBorder, RightTriggerFill, RightTriggerLbl, RightTriggerVal,
                input.Trigger.Pull, 45.0, input.Trigger.Touch, input.Trigger.Click);

            RightGripPullVal.Text = input.Grip.Pull.ToString("F2");
            SetGaugeVisual(RightGripBorder, RightGripFill, RightGripLbl, RightGripVal,
                input.Grip.Pull, 39.0, false, input.Grip.Click);
            RightGripVal.Text = _module.RightGripForce.ToString("F2");
            SetGripForceOverlay(RightGripForceFill, input.Grip.Pull, _module.RightGripForce, 39.0);

            SetStick(RightStickBorder, RightStickDot, RightStickCoord,
                input.Stick.Position.X, input.Stick.Position.Y, input.Stick.Touch, input.Stick.Click);

            SetPad(RightPadBorder, RightPadDot, RightPadCoord,
                input.Pad.Position.X, input.Pad.Position.Y, input.Pad.Touch, input.Pad.Click);
        }
    }

    private static void SetBar(ProgressBar bar, TextBlock text, float value)
    {
        bar.Value = value;
        text.Text = value.ToString("F2");
    }

    private static void SetGaugeVisual(Border border, Border fill, TextBlock label,
        TextBlock valueText, float value, double maxHeight, bool touch, bool click)
    {
        fill.Height = Math.Clamp(value, 0f, 1f) * maxHeight;
        valueText.Text = value.ToString("F2");

        if (click)
        {
            border.BorderBrush = FuviiStyles.GoldBrush;
            fill.Background = FuviiStyles.GoldBrush;
            label.Foreground = FuviiStyles.GoldBrush;
        }
        else if (touch)
        {
            border.BorderBrush = FuviiStyles.PurpleBrush;
            fill.Background = FuviiStyles.PurpleBrush;
            label.Foreground = FuviiStyles.PurpleBrush;
        }
        else if (value > 0.01f)
        {
            border.BorderBrush = FuviiStyles.InactiveBrush;
            fill.Background = FuviiStyles.PurpleBrush;
            label.Foreground = FuviiStyles.InactiveBrush;
        }
        else
        {
            border.BorderBrush = FuviiStyles.InactiveBrush;
            fill.Background = FuviiStyles.InactiveBrush;
            label.Foreground = FuviiStyles.InactiveBrush;
        }
    }

    // Gold force overlay: when grip pull is maxed, shows force amount on top of the purple fill.
    private static void SetGripForceOverlay(Border forceFill, float pull, float force, double maxHeight)
    {
        if (pull >= 0.95f && force > 0.01f)
        {
            forceFill.Height = Math.Clamp(force, 0f, 1f) * maxHeight;
            forceFill.Visibility = System.Windows.Visibility.Visible;
        }
        else
        {
            forceFill.Height = 0;
            forceFill.Visibility = System.Windows.Visibility.Collapsed;
        }
    }

    private static void SetButton(Border border, TextBlock label, bool touch, bool click)
    {
        if (click)
        {
            border.Background = FuviiStyles.GoldBrush;
            border.BorderBrush = FuviiStyles.GoldBrush;
            label.Foreground = Brushes.Black;
        }
        else if (touch)
        {
            border.Background = FuviiStyles.PurpleBrush;
            border.BorderBrush = FuviiStyles.PurpleBrush;
            label.Foreground = Brushes.White;
        }
        else
        {
            border.Background = FuviiStyles.DarkBgBrush;
            border.BorderBrush = FuviiStyles.InactiveBrush;
            label.Foreground = FuviiStyles.InactiveBrush;
        }
    }

    private static void SetStick(Border border, Ellipse dot, TextBlock coord,
        float x, float y, bool touch, bool click)
    {
        Canvas.SetLeft(dot, 18 + x * 15);
        Canvas.SetTop(dot, 18 - y * 15);
        coord.Text = $"{x:F2}, {y:F2}";

        if (click)
        {
            border.BorderBrush = FuviiStyles.GoldBrush;
            dot.Fill = FuviiStyles.GoldBrush;
        }
        else if (touch)
        {
            border.BorderBrush = FuviiStyles.PurpleBrush;
            dot.Fill = FuviiStyles.PurpleBrush;
        }
        else
        {
            border.BorderBrush = FuviiStyles.InactiveBrush;
            dot.Fill = FuviiStyles.InactiveBrush;
        }
    }

    private static void SetPad(Border border, Ellipse dot, TextBlock coord,
        float x, float y, bool touch, bool click)
    {
        Canvas.SetLeft(dot, 19 + x * 16);
        Canvas.SetTop(dot, 19 - y * 16);
        coord.Text = $"{x:F2}, {y:F2}";

        if (click)
        {
            border.BorderBrush = FuviiStyles.GoldBrush;
            dot.Fill = FuviiStyles.GoldBrush;
        }
        else if (touch)
        {
            border.BorderBrush = FuviiStyles.PurpleBrush;
            dot.Fill = FuviiStyles.PurpleBrush;
        }
        else
        {
            border.BorderBrush = FuviiStyles.InactiveBrush;
            dot.Fill = FuviiStyles.InactiveBrush;
        }
    }

    private static readonly Dictionary<string, DeviceRole> RoleToDeviceRole = new()
    {
        ["Chest"] = DeviceRole.Chest,
        ["Waist"] = DeviceRole.Waist,
        ["LeftFoot"] = DeviceRole.LeftFoot,
        ["RightFoot"] = DeviceRole.RightFoot,
        ["LeftKnee"] = DeviceRole.LeftKnee,
        ["RightKnee"] = DeviceRole.RightKnee,
        ["LeftElbow"] = DeviceRole.LeftElbow,
        ["RightElbow"] = DeviceRole.RightElbow,
    };

    // Connected = purple border + battery fill, pressed = gold, inactive = gray.
    private void UpdateTrackers()
    {
        IReadOnlyList<TrackerButtonState> trackerStates = _module.TrackerButtonStates;

        HashSet<string> detectedRoles = new();
        HashSet<string> pressedRoles = new();
        Dictionary<string, float> batteryLevels = new();

        try
        {
            OpenVRManager manager = _module.GetOpenVRManager();
            foreach (var (roleName, deviceRole) in RoleToDeviceRole)
            {
                TrackedDevice? device = manager.GetTrackedDevice(deviceRole);
                if (device?.IsConnected == true)
                {
                    detectedRoles.Add(roleName);
                    batteryLevels[roleName] = device.BatteryPercentage;
                }
            }
        }
        catch (Exception ex)
        {
            _module.LogDebug($"SDK tracker detection failed: {ex.Message}");
        }

        if (trackerStates is not null)
        {
            foreach (TrackerButtonState s in trackerStates)
            {
                detectedRoles.Add(s.Role);
                if (s.ButtonPressed)
                    pressedRoles.Add(s.Role);
            }
        }

        IReadOnlyDictionary<string, float> moduleBatteries = _module.TrackerBatteryLevels;
        foreach (var (role, pct) in moduleBatteries)
        {
            if (!batteryLevels.ContainsKey(role))
            {
                batteryLevels[role] = pct;
                detectedRoles.Add(role);
            }
        }

        foreach (TrackerGroupDef group in TrackerGroupDefs)
        {
            bool groupDetected;
            if (group.IsPaired)
                groupDetected = detectedRoles.Contains(group.LeftRole!) || detectedRoles.Contains(group.RightRole!);
            else
                groupDetected = detectedRoles.Contains(group.SingleRole!);

            if (_trackerRows.TryGetValue(group.DisplayName, out var rowGrid))
                rowGrid.Visibility = (group.AlwaysVisible || groupDetected) ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach (var (role, icon) in _trackerIcons)
        {
            bool detected = detectedRoles.Contains(role);
            bool pressed = pressedRoles.Contains(role);
            Rectangle? batteryFill = _trackerBatteryFills.GetValueOrDefault(role);
            TextBlock? tb = (icon.Child as Grid)?.Children.OfType<TextBlock>().FirstOrDefault();

            if (pressed)
            {
                icon.Background = FuviiStyles.GoldBrush;
                icon.BorderBrush = FuviiStyles.GoldBrush;
                if (tb != null) tb.Foreground = Brushes.Black;
                if (batteryFill != null)
                {
                    batteryFill.Fill = Brushes.Transparent;
                    batteryFill.Height = 0;
                }
            }
            else if (detected)
            {
                icon.Background = Brushes.Transparent;
                icon.BorderBrush = FuviiStyles.PurpleBrush;
                if (tb != null) tb.Foreground = FuviiStyles.PurpleBrush;
                // Battery fill drives the background height
                if (batteryFill != null)
                {
                    float pct = batteryLevels.GetValueOrDefault(role, 0f);
                    batteryFill.Fill = FuviiStyles.PurpleFillBrush;
                    batteryFill.Height = Math.Clamp(pct, 0, 1) * TrackerIconSize;
                }
            }
            else
            {
                icon.Background = FuviiStyles.DarkBgBrush;
                icon.BorderBrush = FuviiStyles.InactiveBrush;
                if (tb != null) tb.Foreground = FuviiStyles.InactiveBrush;
                if (batteryFill != null)
                {
                    batteryFill.Fill = Brushes.Transparent;
                    batteryFill.Height = 0;
                }
            }
        }
    }

    private record struct SvgPathDef(VRCGesture Gesture, double Scale, string Path);
    private record struct SvgIconEntry(VRCGesture Gesture, string PathData, double SvgScale, Rect Bounds);
    private record struct ShiftedSvgIcon(VRCGesture Gesture, string PathData, double SvgScale, double Dx, double Dy);
}
