// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// A null implementation of <see cref="IEncryptionTelemetry"/> that discards all metrics.
/// </summary>
/// <remarks>
/// Use this implementation when telemetry collection is not desired or for testing scenarios
/// where metrics collection should be disabled.
/// </remarks>
public sealed class NullEncryptionTelemetry : IEncryptionTelemetry, IEncryptionTelemetryDetails
{
	private static readonly Meter s_nullMeter = new("Excalibur.Dispatch.Encryption.Null", "1.0.0");

	private NullEncryptionTelemetry()
	{
	}

	/// <summary>
	/// Gets the singleton instance of <see cref="NullEncryptionTelemetry"/>.
	/// </summary>
	public static NullEncryptionTelemetry Instance { get; } = new();

	/// <inheritdoc />
	public Meter Meter => s_nullMeter;

	/// <inheritdoc />
	public void RecordOperation(string operation, string algorithm, string status, string provider)
	{
		// No-op
	}

	/// <inheritdoc />
	public void RecordOperationDuration(double durationMs, string operation, string provider)
	{
		// No-op
	}

	/// <inheritdoc />
	public void UpdateProviderHealth(string provider, string status, int score)
	{
		// No-op
	}

	/// <inheritdoc />
	public void RecordFieldsMigrated(long count, string fromProvider, string toProvider, string store)
	{
		// No-op
	}

	/// <inheritdoc />
	public void RecordKeyRotation(string provider, string rotationType)
	{
		// No-op
	}

	/// <inheritdoc />
	public void RecordBytesProcessed(long bytes, string operation, string provider)
	{
		// No-op
	}

	/// <inheritdoc />
	public void RecordCacheAccess(bool hit, string provider)
	{
		// No-op
	}

	/// <inheritdoc />
	public void UpdateActiveKeyCount(int count, string provider)
	{
		// No-op
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		if (serviceType == typeof(IEncryptionTelemetryDetails))
		{
			return this;
		}

		return null;
	}
}
