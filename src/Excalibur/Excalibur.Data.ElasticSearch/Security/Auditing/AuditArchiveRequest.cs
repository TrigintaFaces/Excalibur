// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents an audit archive request.
/// </summary>
public sealed class AuditArchiveRequest
{
	/// <summary>
	/// Gets or sets the cutoff date for audit events to be archived.
	/// </summary>
	/// <value> The cutoff date. Events before this date will be archived. </value>
	public DateTimeOffset CutoffDate { get; set; }

	/// <summary>
	/// Gets or sets the archive location where audit events will be stored.
	/// </summary>
	/// <value> The archive location path or identifier. </value>
	public string ArchiveLocation { get; set; } = string.Empty;
}
