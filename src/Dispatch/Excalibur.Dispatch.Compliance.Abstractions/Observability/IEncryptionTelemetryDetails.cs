// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides detailed encryption telemetry operations beyond core metrics.
/// </summary>
/// <remarks>
/// <para>
/// This sub-interface contains health monitoring, migration tracking, cache metrics,
/// and key inventory operations. Access via <see cref="IEncryptionTelemetry.GetService(Type)"/>
/// with <c>typeof(IEncryptionTelemetryDetails)</c>.
/// </para>
/// <para>
/// <strong>ISP Split (Sprint 551):</strong> Extracted from <see cref="IEncryptionTelemetry"/>
/// to keep the core interface at or below 5 methods per the Microsoft-First Design Standard.
/// </para>
/// </remarks>
public interface IEncryptionTelemetryDetails
{
	/// <summary>
	/// Updates the health status of an encryption provider.
	/// </summary>
	/// <param name="provider">The encryption provider name.</param>
	/// <param name="status">The health status ("healthy", "degraded", "unhealthy").</param>
	/// <param name="score">A numeric health score (0-100).</param>
	void UpdateProviderHealth(string provider, string status, int score);

	/// <summary>
	/// Records fields migrated between encryption providers.
	/// </summary>
	/// <param name="count">The number of fields migrated.</param>
	/// <param name="fromProvider">The source provider name.</param>
	/// <param name="toProvider">The target provider name.</param>
	/// <param name="store">The data store name.</param>
	void RecordFieldsMigrated(long count, string fromProvider, string toProvider, string store);

	/// <summary>
	/// Records a cache hit or miss for encryption keys.
	/// </summary>
	/// <param name="hit">Whether the cache was hit.</param>
	/// <param name="provider">The encryption provider name.</param>
	void RecordCacheAccess(bool hit, string provider);

	/// <summary>
	/// Updates the count of active encryption keys.
	/// </summary>
	/// <param name="count">The number of active keys.</param>
	/// <param name="provider">The encryption provider name.</param>
	void UpdateActiveKeyCount(int count, string provider);
}
