// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Event arguments for when audit log archiving operations are completed.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuditArchiveCompletedEventArgs" /> class.
/// </remarks>
/// <param name="archivedCount"> The number of audit records that were archived. </param>
/// <param name="startTime"> The start time of the archive operation. </param>
/// <param name="endTime"> The end time of the archive operation. </param>
public sealed class AuditArchiveCompletedEventArgs(long archivedCount, DateTimeOffset startTime, DateTimeOffset endTime) : EventArgs
{
	/// <summary>
	/// Gets the number of audit records that were archived.
	/// </summary>
	/// <value> The total count of audit records successfully processed and archived. </value>
	public long ArchivedCount { get; } = archivedCount;

	/// <summary>
	/// Gets the start time of the archive operation.
	/// </summary>
	/// <value> The UTC timestamp when the archive operation began. </value>
	public DateTimeOffset StartTime { get; } = startTime;

	/// <summary>
	/// Gets the end time of the archive operation.
	/// </summary>
	/// <value> The UTC timestamp when the archive operation completed. </value>
	public DateTimeOffset EndTime { get; } = endTime;

	/// <summary>
	/// Gets the duration of the archive operation.
	/// </summary>
	/// <value> The total time span from start to completion of the archive operation. </value>
	public TimeSpan Duration => EndTime - StartTime;

	/// <summary>
	/// Gets the archive destination path or identifier.
	/// </summary>
	/// <value> The file path, storage location, or identifier where the archived data was stored. </value>
	public string? ArchiveDestination { get; init; }

	/// <summary>
	/// Gets the size of the archived data in bytes.
	/// </summary>
	/// <value> The total size in bytes of the archived data, or null if size information is not available. </value>
	public long? ArchiveSize { get; init; }

	/// <summary>
	/// Gets the compression ratio achieved during archiving.
	/// </summary>
	/// <value> The compression ratio as a decimal (e.g., 0.5 for 50% compression), or null if compression was not applied. </value>
	public double? CompressionRatio { get; init; }

	/// <summary>
	/// Gets any errors that occurred during the archive operation.
	/// </summary>
	/// <value> A read-only list of error messages encountered during archiving, or null if no errors occurred. </value>
	public IReadOnlyList<string>? Errors { get; init; }
}
