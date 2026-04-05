// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Defines the contract for compliance metrics collection.
/// </summary>
/// <remarks>
/// Provides OpenTelemetry-based metrics for compliance features including:
/// <list type="bullet">
///   <item><description>Key rotation tracking and failures</description></item>
///   <item><description>Encryption operation latency and throughput</description></item>
///   <item><description>Audit event logging statistics</description></item>
///   <item><description>Key expiration monitoring</description></item>
/// </list>
/// </remarks>
public interface IComplianceMetrics
{
	/// <summary>
	/// Gets the meter instance for compliance metrics.
	/// </summary>
	Meter Meter { get; }

	/// <summary>
	/// Records a successful key rotation.
	/// </summary>
	/// <param name="keyId">The identifier of the rotated key.</param>
	/// <param name="provider">The KMS provider name (e.g., "AzureKeyVault", "HashiCorpVault").</param>
	void RecordKeyRotation(string keyId, string provider);

	/// <summary>
	/// Records a failed key rotation attempt.
	/// </summary>
	/// <param name="keyId">The identifier of the key that failed to rotate.</param>
	/// <param name="provider">The KMS provider name.</param>
	/// <param name="errorType">The type of error that occurred.</param>
	void RecordKeyRotationFailure(string keyId, string provider, string errorType);

	/// <summary>
	/// Records encryption operation latency.
	/// </summary>
	/// <param name="durationMs">The duration in milliseconds.</param>
	/// <param name="operation">The operation type (e.g., "Encrypt", "Decrypt").</param>
	/// <param name="provider">The KMS provider name.</param>
	/// <param name="success">Whether the operation succeeded.</param>
	void RecordEncryptionLatency(double durationMs, string operation, string provider, bool success);

	/// <summary>
	/// Records an encryption operation.
	/// </summary>
	/// <param name="operation">The operation type (e.g., "Encrypt", "Decrypt").</param>
	/// <param name="provider">The KMS provider name.</param>
	/// <param name="sizeBytes">The size of the data processed in bytes.</param>
	void RecordEncryptionOperation(string operation, string provider, long sizeBytes);

}
