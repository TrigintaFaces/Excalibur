// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Represents a data task request stored in the system for processing.
/// </summary>
/// <remarks> A data task request is used to track the progress and state of a processing job for a specific record type. </remarks>
public sealed class DataTaskRequest
{
	/// <summary>
	/// Gets the unique identifier for the data task.
	/// </summary>
	/// <value> A <see cref="Guid" /> representing the ID of the data task. </value>
	public Guid DataTaskId { get; init; } = Guid.NewGuid();

	/// <summary>
	/// Gets the timestamp when the data task request was created.
	/// </summary>
	/// <value> A <see cref="DateTimeOffset" /> in UTC representing the creation time of the data task. </value>
	public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the type of record associated with the data task request.
	/// </summary>
	/// <value> A string representing the record type for the task. </value>
	/// <remarks> The record type corresponds to the type of data the task processes. It must not be empty. </remarks>
	public string RecordType { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the number of attempts made to process the data task.
	/// </summary>
	/// <value> An integer representing the count of processing attempts. </value>
	public int Attempts { get; set; }

	/// <summary>
	/// Gets the maximum number of processing attempts allowed for the data task.
	/// </summary>
	/// <value> An integer representing the maximum allowed processing attempts. </value>
	public int MaxAttempts { get; init; }

	/// <summary>
	/// Gets the total number of records completed in this data task.
	/// </summary>
	/// <value> An integer representing the count of completed records. </value>
	public int CompletedCount { get; init; }
}
