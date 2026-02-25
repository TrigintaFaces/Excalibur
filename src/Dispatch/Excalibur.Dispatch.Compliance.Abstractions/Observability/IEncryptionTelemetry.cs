// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Defines the contract for encryption-specific telemetry collection.
/// </summary>
/// <remarks>
/// <para>
/// Provides OpenTelemetry-compatible metrics for core encryption operations including:
/// </para>
/// <list type="bullet">
///   <item><description>Operation counters with algorithm and status tags</description></item>
///   <item><description>Operation duration histograms by provider</description></item>
///   <item><description>Key rotation counters</description></item>
///   <item><description>Bytes processed counters</description></item>
/// </list>
/// <para>
/// For detailed telemetry (health, migration, cache, key count), use
/// <see cref="GetService"/> with <c>typeof(IEncryptionTelemetryDetails)</c>.
/// </para>
/// <para>
/// <strong>ISP Split (Sprint 551):</strong> UpdateProviderHealth, RecordFieldsMigrated,
/// RecordCacheAccess, and UpdateActiveKeyCount moved to <see cref="IEncryptionTelemetryDetails"/>
/// to keep the core interface at or below 5 methods.
/// </para>
/// <para>
/// Implementations should use System.Diagnostics.Metrics for OpenTelemetry compatibility.
/// </para>
/// </remarks>
public interface IEncryptionTelemetry
{
	/// <summary>
	/// Gets the meter instance for encryption telemetry.
	/// </summary>
	Meter Meter { get; }

	/// <summary>
	/// Records an encryption operation.
	/// </summary>
	/// <param name="operation">The operation type (e.g., "Encrypt", "Decrypt").</param>
	/// <param name="algorithm">The encryption algorithm used (e.g., "AES-256-GCM").</param>
	/// <param name="status">The operation status ("success" or "failure").</param>
	/// <param name="provider">The encryption provider name.</param>
	void RecordOperation(string operation, string algorithm, string status, string provider);

	/// <summary>
	/// Records the duration of an encryption operation.
	/// </summary>
	/// <param name="durationMs">The duration in milliseconds.</param>
	/// <param name="operation">The operation type.</param>
	/// <param name="provider">The encryption provider name.</param>
	void RecordOperationDuration(double durationMs, string operation, string provider);

	/// <summary>
	/// Records a key rotation event.
	/// </summary>
	/// <param name="provider">The encryption provider name.</param>
	/// <param name="rotationType">The type of rotation ("scheduled", "manual", "emergency").</param>
	void RecordKeyRotation(string provider, string rotationType);

	/// <summary>
	/// Records encryption operation bytes processed.
	/// </summary>
	/// <param name="bytes">The number of bytes processed.</param>
	/// <param name="operation">The operation type.</param>
	/// <param name="provider">The encryption provider name.</param>
	void RecordBytesProcessed(long bytes, string operation, string provider);

	/// <summary>
	/// Gets a sub-interface or service from this telemetry provider.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve (e.g., <c>typeof(IEncryptionTelemetryDetails)</c>).</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType);
}
