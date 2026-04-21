// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Per-projection configuration options for controlling projection behavior.
/// </summary>
/// <remarks>
/// <para>
/// Use via <see cref="IProjectionBuilder{TProjection}.WithOptions"/> to configure
/// per-projection settings:
/// </para>
/// <code>
/// builder.AddProjection&lt;OrderSummary&gt;(p => p
///     .Inline()
///     .WithOptions(o => o.WarningThreshold = TimeSpan.FromMilliseconds(200))
///     .When&lt;OrderPlaced&gt;((proj, e) => { /* ... */ }));
/// </code>
/// </remarks>
public sealed class ProjectionOptions
{
	/// <summary>
	/// Gets or sets the warning threshold for slow projection processing.
	/// If a projection batch exceeds this duration, a warning log is emitted (R27.49).
	/// </summary>
	/// <value>The warning threshold. Default is 100 milliseconds.</value>
	public TimeSpan WarningThreshold { get; set; } = TimeSpan.FromMilliseconds(100);
}
