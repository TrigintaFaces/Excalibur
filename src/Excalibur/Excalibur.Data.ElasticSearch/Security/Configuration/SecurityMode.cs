// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the security enforcement modes for Elasticsearch operations.
/// </summary>
public enum SecurityMode
{
	/// <summary>
	/// Permissive mode with minimal security enforcement. Suitable for development only.
	/// </summary>
	Permissive = 0,

	/// <summary>
	/// Standard mode with balanced security and operational flexibility.
	/// </summary>
	Standard = 1,

	/// <summary>
	/// Strict mode with maximum security enforcement and minimal risk tolerance.
	/// </summary>
	Strict = 2,

	/// <summary>
	/// Compliance mode with additional controls for regulatory requirements.
	/// </summary>
	Compliance = 3,
}
