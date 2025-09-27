using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace BinanceUsdtTicker.Helpers;

/// <summary>
///     Lightweight alternative to the removed DevExpress theme helper.
///     Handles swapping theme dictionaries in the application resources.
/// </summary>
public static class ThemeResourceManager
{
    private static readonly string[] ThemeSuffixes =
    {
        "/Light.xaml",
        "/Dark.xaml",
        "Themes/Light.xaml",
        "Themes/Dark.xaml"
    };

    public static void ApplyTheme(ResourceDictionary resources, ThemeKind theme)
    {
        if (resources == null) return;

        var uri = new Uri($"Themes/{(theme == ThemeKind.Dark ? "Dark" : "Light")}.xaml", UriKind.Relative);
        SwapThemeDictionary(resources.MergedDictionaries, uri);
    }

    public static void ApplyApplicationTheme(ThemeKind theme, Window? excludeWindow = null)
    {
        if (Application.Current == null) return;

        ApplyTheme(Application.Current.Resources, theme);

        foreach (Window window in Application.Current.Windows)
        {
            if (ReferenceEquals(window, excludeWindow))
                continue;

            if (window?.Resources != null)
            {
                ApplyTheme(window.Resources, theme);
            }
        }
    }

    private static void SwapThemeDictionary(IList<ResourceDictionary> dictionaries, Uri newTheme)
    {
        if (dictionaries == null) return;

        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            if (IsThemeDictionary(dictionaries[i]))
            {
                dictionaries.RemoveAt(i);
            }
        }

        dictionaries.Add(new ResourceDictionary { Source = newTheme });
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        if (string.IsNullOrWhiteSpace(source)) return false;

        return ThemeSuffixes.Any(suffix => source.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
