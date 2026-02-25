// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Views;

/// <summary>
/// Configuration options for materialized views.
/// </summary>
public sealed class MaterializedViewOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to catch up all views on application startup.
	/// </summary>
	/// <value><see langword="true"/> to enable catch-up on startup; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool CatchUpOnStartup { get; set; }

	/// <summary>
	/// Gets or sets the batch size for event processing.
	/// </summary>
	/// <value>The number of events to process per batch. Default is 100.</value>
	[Range(1, 10000)]
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the delay between catch-up batches.
	/// </summary>
	/// <value>The delay between batches. Default is 10 milliseconds.</value>
	public TimeSpan BatchDelay { get; set; } = TimeSpan.FromMilliseconds(10);
}
