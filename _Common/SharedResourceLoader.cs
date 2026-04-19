using System;
using System.Runtime.CompilerServices;
using System.Windows;

namespace FuviiOSC.Common;

internal static class SharedResourceLoader
{
    private static bool _loaded;

#pragma warning disable CA2255 // Loading shared XAML resources at assembly load is intentional (to avoid ugly XAML merged dictionaries and referencing shared resources)
    [ModuleInitializer]
    internal static void Init()
#pragma warning restore CA2255
    {
        if (_loaded) return;
        _loaded = true;

        if (Application.Current is null) return;

        var dict = new ResourceDictionary
        {
            Source = new Uri("/FuviiOSC;component/_Common/FuviiStyles.xaml", UriKind.Relative)
        };
        Application.Current.Resources.MergedDictionaries.Add(dict);
    }
}
