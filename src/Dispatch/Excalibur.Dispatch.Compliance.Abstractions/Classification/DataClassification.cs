// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents the sensitivity classification level for data.
/// </summary>
/// <remarks>
/// <para>
/// Data classification is a 4-level system:
/// - Public: Non-sensitive data that can be freely shared
/// - Internal: Business data for internal use only
/// - Confidential: Sensitive business data requiring protection
/// - Restricted: Highest sensitivity (PII, financial, health records)
/// </para>
/// <para> Classification determines encryption requirements, retention policies, and audit logging levels. </para>
/// </remarks>
public enum DataClassification
{
	/// <summary>
	/// Non-sensitive data that can be freely shared externally. No encryption required, minimal audit logging.
	/// </summary>
	Public = 0,

	/// <summary>
	/// Business data intended for internal use only. Encryption at rest recommended, standard audit logging.
	/// </summary>
	Internal = 1,

	/// <summary>
	/// Sensitive business data requiring protection. Encryption required at rest and in transit, enhanced audit logging.
	/// </summary>
	Confidential = 2,

	/// <summary>
	/// Highest sensitivity level for PII, financial, or health records. Field-level encryption required, comprehensive audit logging,
	/// strict access controls, GDPR right-to-erasure support.
	/// </summary>
	Restricted = 3
}
