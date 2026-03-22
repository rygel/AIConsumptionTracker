// <copyright file="RelativeTimeConverter.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Globalization;
using System.Windows.Data;
using AIUsageTracker.Core.Models;

namespace AIUsageTracker.UI.Slim.Converters;

/// <summary>
/// Converts a DateTime value to a relative time string (e.g., "5m", "2h", "3d").
/// </summary>
public class RelativeTimeConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets a value indicating whether to include parentheses around the output.
    /// </summary>
    public bool IncludeParentheses { get; set; }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        DateTime? dateTime = value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            _ => null,
        };

        if (!dateTime.HasValue)
        {
            return null;
        }

        var relativeTime = UsageMath.FormatRelativeTime(dateTime.Value);

        if (string.IsNullOrEmpty(relativeTime))
        {
            return null;
        }

        return this.IncludeParentheses ? $"({relativeTime})" : relativeTime;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
