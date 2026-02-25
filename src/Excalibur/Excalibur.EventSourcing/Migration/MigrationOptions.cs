// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Migration;

/// <summary>
/// Configuration options for event batch migration.
/// </summary>
/// <remarks>
/// <para>
/// These options control how events are read from source streams and written to target streams
/// during migration. Use <see cref="DryRun"/> to validate a migration plan without writing.
/// </para>
/// </remarks>
public sealed class MigrationOptions
{
	/// <summary>
	/// Gets or sets the number of events to process per batch.
	/// </summary>
	/// <value>The batch size. Default is 500.</value>
	[Range(1, 100000)]
	public int BatchSize { get; set; } = 500;

	/// <summary>
	/// Gets or sets the source stream pattern to match (e.g., "Order-*").
	/// </summary>
	/// <value>The source stream pattern. Null matches all streams.</value>
	public string? SourceStreamPattern { get; set; }

	/// <summary>
	/// Gets or sets the prefix for target stream names.
	/// </summary>
	/// <value>The target stream prefix. Null preserves original stream names.</value>
	public string? TargetStreamPrefix { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to perform a dry run without writing events.
	/// </summary>
	/// <value><see langword="true"/> to perform a dry run; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool DryRun { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of events to migrate. Zero means no limit.
	/// </summary>
	/// <value>The maximum event count. Default is 0 (no limit).</value>
	[Range(0, int.MaxValue)]
	public int MaxEvents { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to continue processing on individual event errors.
	/// </summary>
	/// <value><see langword="true"/> to continue on error; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool ContinueOnError { get; set; }
}
