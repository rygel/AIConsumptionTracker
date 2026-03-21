// <copyright file="IChangelogMarkdownRendererFactory.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows.Documents;
using System.Windows.Media;

namespace AIUsageTracker.UI.Slim.Services;

public interface IChangelogMarkdownRendererFactory
{
    Func<string, FlowDocument> Create(Func<string, SolidColorBrush, SolidColorBrush> resolveResourceBrush);
}
