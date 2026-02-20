// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines data sensitivity classification levels.
/// </summary>
public enum DataClassification
{
	/// <summary>
	/// Public data that can be freely shared.
	/// </summary>
	Public = 0,

	/// <summary>
	/// Internal data for organizational use only.
	/// </summary>
	Internal = 1,

	/// <summary>
	/// Confidential data requiring protection.
	/// </summary>
	Confidential = 2,

	/// <summary>
	/// Restricted data requiring highest protection level.
	/// </summary>
	Restricted = 3,

	/// <summary>
	/// Personally Identifiable Information (PII).
	/// </summary>
	PersonallyIdentifiable = 4,

	/// <summary>
	/// Protected Health Information (PHI) under HIPAA.
	/// </summary>
	HealthInformation = 5,
}
