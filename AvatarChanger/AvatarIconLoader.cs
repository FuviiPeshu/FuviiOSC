using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FuviiOSC.AvatarChanger;

public static class AvatarIconLoader
{
    private const string RESOURCE_PREFIX = "AvatarChanger/icons/";
    private static readonly string UserIconFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VRCOSC", "packages", "resources", "AvatarChangerIcons");

    private static List<string>? _cachedKeys;
    private static readonly Dictionary<string, BitmapImage> _imageCache = new();

    public static string GetUserIconFolder()
    {
        Directory.CreateDirectory(UserIconFolder);
        return UserIconFolder;
    }

    public static IReadOnlyList<string> GetAvailableKeys()
    {
        _cachedKeys = new List<string>();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            ResourceManager rm = new("FuviiOSC.g", Assembly.GetExecutingAssembly());
            using ResourceSet? rs = rm.GetResourceSet(CultureInfo.InvariantCulture, true, false);
            if (rs != null)
            {
                foreach (DictionaryEntry entry in rs)
                {
                    string resourceKey = entry.Key?.ToString() ?? string.Empty;
                    if (resourceKey.StartsWith(RESOURCE_PREFIX, StringComparison.OrdinalIgnoreCase) &&
                        resourceKey.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        string iconName = Path.GetFileNameWithoutExtension(resourceKey);
                        if (seen.Add(iconName))
                            _cachedKeys.Add(iconName);
                    }
                }
            }
        }
        catch { }

        Directory.CreateDirectory(UserIconFolder);
        foreach (string file in Directory.GetFiles(UserIconFolder, "*.png"))
        {
            string iconName = Path.GetFileNameWithoutExtension(file);
            if (seen.Add(iconName))
                _cachedKeys.Add(iconName);
        }

        _cachedKeys.Sort(StringComparer.OrdinalIgnoreCase);
        return _cachedKeys;
    }

    public static BitmapImage? GetIcon(string iconKey)
    {
        if (string.IsNullOrEmpty(iconKey)) return null;

        if (_imageCache.TryGetValue(iconKey, out BitmapImage? cached))
            return cached;

        BitmapImage? image = TryLoadEmbedded(iconKey);
        if (image == null)
            image = TryLoadFromDisk(iconKey);
        if (image != null)
            _imageCache[iconKey] = image;

        return image;
    }

    private static BitmapImage? TryLoadEmbedded(string iconKey)
    {
        try
        {
            Uri uri = new($"pack://application:,,,/FuviiOSC;component/AvatarChanger/icons/{iconKey}.png", UriKind.Absolute);
            System.Windows.Resources.StreamResourceInfo? sri = Application.GetResourceStream(uri);
            if (sri == null) return null;

            BitmapImage image = new();
            image.BeginInit();
            image.StreamSource = sri.Stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 64;
            image.EndInit();
            image.Freeze();
            sri.Stream.Dispose();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? TryLoadFromDisk(string iconKey)
    {
        string filePath = Path.Combine(UserIconFolder, $"{iconKey}.png");
        if (!File.Exists(filePath)) return null;

        try
        {
            BitmapImage image = new();
            image.BeginInit();
            image.UriSource = new Uri(filePath, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 64;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public static string? ImportIcon(string sourceFilePath)
    {
        Directory.CreateDirectory(UserIconFolder);

        string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }

        string destPath = Path.Combine(UserIconFolder, $"{fileName}.png");
        int counter = 1;
        while (File.Exists(destPath))
        {
            fileName = $"{Path.GetFileNameWithoutExtension(sourceFilePath)}_{counter}";
            destPath = Path.Combine(UserIconFolder, $"{fileName}.png");
            counter++;
        }

        File.Copy(sourceFilePath, destPath);
        _cachedKeys = null;
        _imageCache.Remove(fileName);

        return fileName;
    }

    public static void InvalidateCache()
    {
        _cachedKeys = null;
        _imageCache.Clear();
    }
}
