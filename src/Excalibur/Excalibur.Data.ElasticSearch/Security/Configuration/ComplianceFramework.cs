// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines supported compliance frameworks.
/// </summary>
public enum ComplianceFramework
{
	/// <summary>
	/// General Data Protection Regulation (GDPR).
	/// </summary>
	Gdpr = 0,

	/// <summary>
	/// Health Insurance Portability and Accountability Act (HIPAA).
	/// </summary>
	Hipaa = 1,

	/// <summary>
	/// Sarbanes-Oxley Act (SOX).
	/// </summary>
	Sox = 2,

	/// <summary>
	/// Payment Card Industry Data Security Standard (PCI DSS).
	/// </summary>
	PciDss = 3,

	/// <summary>
	/// ISO 27001 Information Security Management.
	/// </summary>
	Iso27001 = 4,

	/// <summary>
	/// NIST Cybersecurity Framework.
	/// </summary>
	NistCsf = 5,

	/// <summary>
	/// Federal Information Security Management Act (FISMA).
	/// </summary>
	Fisma = 6,
}
