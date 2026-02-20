// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Excalibur.EventSourcing.Migration;

/// <summary>
/// Configuration options for the migration runner.
/// </summary>
public sealed class MigrationRunnerOptions
{
	/// <summary>
	/// Gets or sets the assembly containing migration definitions.
	/// </summary>
	/// <value>The migration assembly. When <see langword="null"/>, the entry assembly is used.</value>
	public Assembly? MigrationAssembly { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to perform a dry run without applying changes.
	/// </summary>
	/// <value><see langword="true"/> to perform a dry run; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool DryRun { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to continue processing when an error occurs.
	/// </summary>
	/// <value><see langword="true"/> to continue on error; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool ContinueOnError { get; set; }

	/// <summary>
	/// Gets or sets the number of streams to process in parallel.
	/// </summary>
	/// <value>The parallelism degree. Default is 1 (sequential).</value>
	[Range(1, 32)]
	public int ParallelStreams { get; set; } = 1;

	/// <summary>
	/// Gets or sets the batch size for event processing during migration.
	/// </summary>
	/// <value>The batch size. Default is 500.</value>
	[Range(1, 100000)]
	public int BatchSize { get; set; } = 500;
}
