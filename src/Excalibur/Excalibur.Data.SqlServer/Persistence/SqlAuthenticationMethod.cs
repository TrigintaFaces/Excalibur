// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Persistence;

/// <summary>
/// Specifies the SQL authentication method.
/// </summary>
public enum SqlAuthenticationMethod
{
	/// <summary>
	/// No authentication method specified.
	/// </summary>
	NotSpecified = 0,

	/// <summary>
	/// SQL Server authentication.
	/// </summary>
	SqlPassword = 1,

	/// <summary>
	/// Active Directory integrated authentication.
	/// </summary>
	ActiveDirectoryIntegrated = 2,

	/// <summary>
	/// Active Directory password authentication.
	/// </summary>
	ActiveDirectoryPassword = 3,

	/// <summary>
	/// Active Directory interactive authentication.
	/// </summary>
	ActiveDirectoryInteractive = 4,

	/// <summary>
	/// Active Directory service principal authentication.
	/// </summary>
	ActiveDirectoryServicePrincipal = 5,

	/// <summary>
	/// Active Directory device code flow authentication.
	/// </summary>
	ActiveDirectoryDeviceCodeFlow = 6,

	/// <summary>
	/// Active Directory managed identity authentication.
	/// </summary>
	ActiveDirectoryManagedIdentity = 7,

	/// <summary>
	/// Active Directory MSI authentication.
	/// </summary>
	ActiveDirectoryMSI = 8,

	/// <summary>
	/// Active Directory default authentication.
	/// </summary>
	ActiveDirectoryDefault = 9,
}
