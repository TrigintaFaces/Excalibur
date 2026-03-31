// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

namespace CdcJobQuartz.Infrastructure;

/// <summary>
/// Configuration options for projection processing.
/// </summary>
public sealed class ProjectionOptions
{
	/// <summary>
	/// Configuration section name.
	/// </summary>
	public const string SectionName = "Projections";

	/// <summary>Gets or sets the polling interval for new events.</summary>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>Gets or sets the batch size for event processing.</summary>
	public int BatchSize { get; set; } = 100;

	/// <summary>Gets or sets whether to rebuild projections on startup.</summary>
	public bool RebuildOnStartup { get; set; }
}

// NOTE: ProjectionBackgroundService has been removed.
// Projection processing is now handled by the framework's inline projection pipeline
// registered via AddExcaliburEventSourcing -> AddProjection<T>().Inline().WhenHandledBy<>().
// See Program.cs for the registration.
