// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents an audit log integrity result.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuditLogIntegrityResult" /> class.
/// </remarks>
/// <param name="isValid"> A value indicating whether the audit log integrity check passed. </param>
/// <param name="totalEvents"> The total number of events checked. </param>
/// <param name="corruptedEvents"> The number of corrupted events found. </param>
/// <param name="message"> A descriptive message about the integrity check result. </param>
public sealed class AuditLogIntegrityResult(bool isValid, int totalEvents, int corruptedEvents, string message)
{
	/// <summary>
	/// Gets a value indicating whether the audit log integrity check passed.
	/// </summary>
	/// <value> <c> true </c> if the audit log is valid; otherwise, <c> false </c>. </value>
	public bool IsValid { get; } = isValid;

	/// <summary>
	/// Gets the total number of events that were checked for integrity.
	/// </summary>
	/// <value> The total number of audit events checked. </value>
	public int TotalEvents { get; } = totalEvents;

	/// <summary>
	/// Gets the number of corrupted events found during the integrity check.
	/// </summary>
	/// <value> The number of corrupted audit events. </value>
	public int CorruptedEvents { get; } = corruptedEvents;

	/// <summary>
	/// Gets a descriptive message about the integrity check result.
	/// </summary>
	/// <value> A message describing the audit log integrity check outcome. </value>
	public string Message { get; } = message;
}
