// <copyright file="IWpfProviderIconServiceFactory.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows;
using System.Windows.Media;

namespace AIUsageTracker.UI.Slim.Services;

public interface IWpfProviderIconServiceFactory
{
    Func<string, FrameworkElement> Create(Func<string, SolidColorBrush, SolidColorBrush> resolveResourceBrush);
}
