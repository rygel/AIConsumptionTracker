// <copyright file="WindowPreferencePresentationCatalogTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows;

using AIUsageTracker.Core.Models;
using AIUsageTracker.UI.Slim;

namespace AIUsageTracker.Tests.UI;

public sealed class WindowPreferencePresentationCatalogTests
{
    [Fact]
    public void Create_WithConfiguredFontAndStyles_MapsAllValues()
    {
        var preferences = new AppPreferences
        {
            AlwaysOnTop = true,
            WindowWidth = 420,
            WindowHeight = 520,
            FontFamily = "Consolas",
            FontSize = 13,
            FontBold = true,
            FontItalic = true,
        };

        var presentation = WindowPreferencePresentationCatalog.Create(preferences);

        Assert.True(presentation.Topmost);
        Assert.Equal(420, presentation.Width);
        Assert.Equal(520, presentation.Height);
        Assert.Equal("Consolas", presentation.FontFamilyName);
        Assert.Equal(13, presentation.FontSize);
        Assert.Equal(FontWeights.Bold, presentation.FontWeight);
        Assert.Equal(FontStyles.Italic, presentation.FontStyle);
        Assert.True(presentation.AlwaysOnTopChecked);
    }

    [Fact]
    public void Create_WithoutValidFontAndSize_UsesNullAndNormalStyles()
    {
        var preferences = new AppPreferences
        {
            AlwaysOnTop = false,
            WindowWidth = 360,
            WindowHeight = 480,
            FontFamily = " ",
            FontSize = 0,
            FontBold = false,
            FontItalic = false,
        };

        var presentation = WindowPreferencePresentationCatalog.Create(preferences);

        Assert.False(presentation.Topmost);
        Assert.Equal(360, presentation.Width);
        Assert.Equal(480, presentation.Height);
        Assert.Null(presentation.FontFamilyName);
        Assert.Null(presentation.FontSize);
        Assert.Equal(FontWeights.Normal, presentation.FontWeight);
        Assert.Equal(FontStyles.Normal, presentation.FontStyle);
        Assert.False(presentation.AlwaysOnTopChecked);
    }
}
