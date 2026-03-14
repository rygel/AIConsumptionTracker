// <copyright file="WindowKind.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

namespace AIUsageTracker.Core.Models;

/// <summary>
/// Identifies the type of quota window for usage tracking.
/// </summary>
/// <remarks>
/// <para>Semantic meanings:</para>
/// <list type="bullet">
/// <item><description>Burst (Primary): Short-term burst limit, e.g., 3-hour or 5-hour quota</description></item>
/// <item><description>Rolling (Secondary): Long-term rolling limit, e.g., 7-day or weekly quota</description></item>
/// <item><description>ModelSpecific (Spark): Model-specific limit, e.g., Codex Spark model quota</description></item>
/// </list>
/// </remarks>
public enum WindowKind
{
    /// <summary>
    /// No specific window type.
    /// </summary>
    None = 0,

    /// <summary>
    /// Short-term burst limit window (e.g., 3-hour or 5-hour quota).
    /// Semantically equivalent to <see cref="Burst"/>.
    /// </summary>
    [Obsolete("Use WindowKind.Burst instead.")]
    Primary = 1,

    /// <summary>
    /// Long-term rolling limit window (e.g., 7-day or weekly quota).
    /// Semantically equivalent to <see cref="Rolling"/>.
    /// </summary>
    [Obsolete("Use WindowKind.Rolling instead.")]
    Secondary = 2,

    /// <summary>
    /// Model-specific limit window (e.g., Codex Spark model quota).
    /// Semantically equivalent to <see cref="ModelSpecific"/>.
    /// </summary>
    [Obsolete("Use WindowKind.ModelSpecific instead.")]
    Spark = 3,

    /// <summary>
    /// Short-term burst limit window (e.g., 3-hour or 5-hour quota).
    /// Preferred semantic name for <see cref="Primary"/>.
    /// </summary>
#pragma warning disable CS0618 // Aliases reference obsolete members by design
    Burst = Primary,

    /// <summary>
    /// Long-term rolling limit window (e.g., 7-day or weekly quota).
    /// Preferred semantic name for <see cref="Secondary"/>.
    /// </summary>
    Rolling = Secondary,

    /// <summary>
    /// Model-specific limit window (e.g., Codex Spark model quota).
    /// Preferred semantic name for <see cref="Spark"/>.
    /// </summary>
    ModelSpecific = Spark,
#pragma warning restore CS0618
}
