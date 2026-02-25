// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the status of a security incident investigation.
/// </summary>
public enum SecurityIncidentStatus
{
	/// <summary>
	/// The incident is newly reported and not yet being investigated.
	/// </summary>
	Open = 0,

	/// <summary>
	/// The incident is currently being investigated.
	/// </summary>
	InProgress = 1,

	/// <summary>
	/// The incident investigation is on hold pending additional information.
	/// </summary>
	OnHold = 2,

	/// <summary>
	/// The incident has been resolved.
	/// </summary>
	Resolved = 3,

	/// <summary>
	/// The incident was determined to be a false positive.
	/// </summary>
	FalsePositive = 4,

	/// <summary>
	/// The incident has been closed without Excalibur.Tests.Integration.
	/// </summary>
	Closed = 5,
}
