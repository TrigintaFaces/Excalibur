// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents a single step in a migration plan.
/// </summary>
public sealed class MigrationStep
{
	/// <summary>
	/// Gets the step number.
	/// </summary>
	/// <value>
	/// The step number.
	/// </value>
	public required int StepNumber { get; init; }

	/// <summary>
	/// Gets the step name.
	/// </summary>
	/// <value>
	/// The step name.
	/// </value>
	public required string Name { get; init; }

	/// <summary>
	/// Gets the step description.
	/// </summary>
	/// <value>
	/// The step description.
	/// </value>
	public required string Description { get; init; }

	/// <summary>
	/// Gets the operation type.
	/// </summary>
	/// <value>
	/// The operation type.
	/// </value>
	public required StepOperationType OperationType { get; init; }

	/// <summary>
	/// Gets a value indicating whether this step is critical (cannot be skipped).
	/// </summary>
	/// <value>
	/// A value indicating whether this step is critical (cannot be skipped).
	/// </value>
	public bool IsCritical { get; init; }

	/// <summary>
	/// Gets the estimated duration for this step.
	/// </summary>
	/// <value>
	/// The estimated duration for this step.
	/// </value>
	public TimeSpan? EstimatedDuration { get; init; }

	/// <summary>
	/// Gets any parameters for the step.
	/// </summary>
	/// <value>
	/// Any parameters for the step.
	/// </value>
	public IDictionary<string, object>? Parameters { get; init; }
}
