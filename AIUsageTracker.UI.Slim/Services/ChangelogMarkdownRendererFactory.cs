// <copyright file="ChangelogMarkdownRendererFactory.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows.Documents;
using System.Windows.Media;

namespace AIUsageTracker.UI.Slim.Services;

public sealed class ChangelogMarkdownRendererFactory : IChangelogMarkdownRendererFactory
{
    public Func<string, FlowDocument> Create(Func<string, SolidColorBrush, SolidColorBrush> resolveResourceBrush)
    {
        var renderer = new ChangelogMarkdownRenderer(resolveResourceBrush);
        return renderer.BuildDocument;
    }
}
