// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents an audit archive result.
/// </summary>
public sealed class AuditArchiveResult
{
	/// <summary>
	/// Gets or sets the unique identifier for the archive operation.
	/// </summary>
	/// <value> The unique archive identifier. </value>
	public Guid ArchiveId { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the archive operation was completed.
	/// </summary>
	/// <value> The archive completion timestamp. </value>
	public DateTimeOffset ArchivedAt { get; set; }

	/// <summary>
	/// Gets or sets the cutoff date used for the archive operation.
	/// </summary>
	/// <value> The cutoff date for archived events. </value>
	public DateTimeOffset CutoffDate { get; set; }

	/// <summary>
	/// Gets or sets the number of events that were archived.
	/// </summary>
	/// <value> The count of archived events. </value>
	public int EventsArchived { get; set; }

	/// <summary>
	/// Gets or sets the size of the archive in bytes.
	/// </summary>
	/// <value> The archive size in bytes. </value>
	public long ArchiveSize { get; set; }

	/// <summary>
	/// Gets or sets the location where the archive was stored.
	/// </summary>
	/// <value> The archive storage location. </value>
	public string ArchiveLocation { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the archive operation was successful.
	/// </summary>
	/// <value> <c> true </c> if the operation was successful; otherwise, <c> false </c>. </value>
	public bool Success { get; set; }

	/// <summary>
	/// Gets or sets the error message if the archive operation failed.
	/// </summary>
	/// <value> The error message, or <c> null </c> if the operation was successful. </value>
	public string? ErrorMessage { get; set; }
}
