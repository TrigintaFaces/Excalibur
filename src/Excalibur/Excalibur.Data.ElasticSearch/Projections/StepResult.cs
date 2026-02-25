// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents the result of a migration step.
/// </summary>
public sealed class StepResult
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
	/// Gets a value indicating whether the step succeeded.
	/// </summary>
	/// <value>
	/// A value indicating whether the step succeeded.
	/// </value>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the duration of the step.
	/// </summary>
	/// <value>
	/// The duration of the step.
	/// </value>
	public required TimeSpan Duration { get; init; }

	/// <summary>
	/// Gets any error message.
	/// </summary>
	/// <value>
	/// Any error message.
	/// </value>
	public string? ErrorMessage { get; init; }
}
