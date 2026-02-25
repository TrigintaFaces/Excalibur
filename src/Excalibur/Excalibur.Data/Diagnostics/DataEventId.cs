// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Diagnostics;

/// <summary>
/// Event IDs for Excalibur.Data abstractions (110000-110999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>110000-110099: Connection String Provider</item>
/// <item>110100-110199: Persistence Configuration</item>
/// <item>110200-110299: Health Check</item>
/// <item>110300-110399: Persistence Provider Factory</item>
/// </list>
/// </remarks>
public static class DataEventId
{
	// ========================================
	// 110000-110099: Connection String Provider
	// ========================================

	/// <summary>Connection string set.</summary>
	public const int ConnectionStringSet = 110000;

	/// <summary>Connection string removed.</summary>
	public const int ConnectionStringRemoved = 110001;

	/// <summary>Refreshing connection strings.</summary>
	public const int RefreshingConnectionStrings = 110002;

	/// <summary>Connection strings refreshed.</summary>
	public const int ConnectionStringsRefreshed = 110003;

	/// <summary>Connection string validation failed.</summary>
	public const int ValidationFailed = 110004;

	/// <summary>Connection strings loaded from configuration.</summary>
	public const int ConnectionStringsLoaded = 110005;

	/// <summary>Connection string resolved from environment.</summary>
	public const int ResolvedFromEnvironment = 110006;

	/// <summary>Connection string references secret store.</summary>
	public const int ReferencesSecretStore = 110007;

	/// <summary>Checking external sources for connection strings.</summary>
	public const int CheckingExternalSources = 110008;

	// ========================================
	// 110100-110199: Persistence Configuration
	// ========================================

	/// <summary>Configuration validated.</summary>
	public const int ConfigurationValidated = 110100;

	/// <summary>Provider type configured.</summary>
	public const int ProviderTypeConfigured = 110101;

	/// <summary>Configuration validation error.</summary>
	public const int ConfigurationValidationError = 110102;

	/// <summary>Configuration validation warning.</summary>
	public const int ConfigurationValidationWarning = 110103;

	// ========================================
	// 110200-110299: Health Check
	// ========================================

	/// <summary>Health check started.</summary>
	public const int HealthCheckStarted = 110200;

	/// <summary>Health check completed.</summary>
	public const int HealthCheckCompleted = 110201;

	/// <summary>Health check failed.</summary>
	public const int HealthCheckFailed = 110202;

	/// <summary>Detailed health check failed.</summary>
	public const int DetailedHealthCheckFailed = 110203;

	// ========================================
	// 110300-110399: Persistence Provider Factory
	// ========================================

	/// <summary>Provider created.</summary>
	public const int ProviderCreated = 110300;

	/// <summary>Provider not found.</summary>
	public const int ProviderNotFound = 110301;

	/// <summary>Provider creation failed.</summary>
	public const int ProviderCreationFailed = 110302;

	/// <summary>Provider registered.</summary>
	public const int ProviderRegistered = 110303;

	/// <summary>Provider unregistered.</summary>
	public const int ProviderUnregistered = 110304;
}
