// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides backup configuration information for SOC 2 AVL-003 compliance validation.
/// </summary>
/// <remarks>
/// <para>
/// Implementations should report the backup infrastructure configuration status.
/// This interface allows the compliance framework to verify backup configuration
/// without depending on specific storage implementations.
/// </para>
/// <para>
/// For event sourcing scenarios, this would typically be implemented by a service
/// that checks for <c>ISnapshotStore</c> registration. For other scenarios,
/// it could check for database backup agents, cloud backup services, etc.
/// </para>
/// </remarks>
public interface IBackupConfigurationProvider
{
	/// <summary>
	/// Gets a value indicating whether backup infrastructure is configured.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if backup/snapshot infrastructure is registered and configured;
	/// otherwise, <see langword="false"/>.
	/// </value>
	bool IsBackupConfigured { get; }

	/// <summary>
	/// Gets the name of the backup provider type, if configured.
	/// </summary>
	/// <value>
	/// The type name of the configured backup provider (e.g., "SqlServerSnapshotStore"),
	/// or <see langword="null"/> if no backup infrastructure is configured.
	/// </value>
	string? BackupProviderName { get; }

	/// <summary>
	/// Gets a description of the backup configuration for evidence collection.
	/// </summary>
	/// <value>
	/// A human-readable description of the backup configuration status,
	/// suitable for inclusion in compliance evidence.
	/// </value>
	string ConfigurationDescription { get; }
}
