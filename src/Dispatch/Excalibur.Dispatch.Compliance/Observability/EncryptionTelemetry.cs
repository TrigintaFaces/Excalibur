// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides OpenTelemetry-compatible telemetry collection for encryption operations.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses the System.Diagnostics.Metrics API which is compatible
/// with OpenTelemetry exporters. Metrics are named following OpenTelemetry semantic
/// conventions with the "dispatch.encryption" prefix.
/// </para>
/// <para>
/// Metrics exported:
/// </para>
/// <list type="bullet">
///   <item><description>dispatch.encryption.operations.total - Counter of operations by type/algorithm/status/provider</description></item>
///   <item><description>dispatch.encryption.operation.duration - Histogram of operation durations</description></item>
///   <item><description>dispatch.encryption.provider.health - Gauge of provider health scores</description></item>
///   <item><description>dispatch.encryption.migration.fields_migrated - Counter of migrated fields</description></item>
///   <item><description>dispatch.encryption.key_rotation.total - Counter of key rotations</description></item>
///   <item><description>dispatch.encryption.bytes.processed - Counter of bytes processed</description></item>
///   <item><description>dispatch.encryption.cache.accesses - Counter of cache hits/misses</description></item>
///   <item><description>dispatch.encryption.keys.active - Gauge of active encryption keys</description></item>
/// </list>
/// </remarks>
public sealed class EncryptionTelemetry : IEncryptionTelemetry, IEncryptionTelemetryDetails, IDisposable
{
	/// <summary>
	/// The meter name for encryption telemetry.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.Encryption";

	/// <summary>
	/// The meter version.
	/// </summary>
	public const string MeterVersion = "1.0.0";

	private readonly Counter<long> _operationsTotal;
	private readonly Histogram<double> _operationDuration;
	private readonly Gauge<int> _providerHealth;
	private readonly Counter<long> _fieldsMigrated;
	private readonly Counter<long> _keyRotationsTotal;
	private readonly Counter<long> _bytesProcessed;
	private readonly Counter<long> _cacheAccesses;
	private readonly Gauge<int> _activeKeys;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionTelemetry"/> class.
	/// </summary>
	public EncryptionTelemetry()
	{
		Meter = new Meter(MeterName, MeterVersion);

		// Operation counter
		_operationsTotal = Meter.CreateCounter<long>(
			"dispatch.encryption.operations.total",
			"count",
			"Total number of encryption operations");

		// Operation duration histogram
		_operationDuration = Meter.CreateHistogram<double>(
			"dispatch.encryption.operation.duration",
			"ms",
			"Duration of encryption operations in milliseconds");

		// Provider health gauge
		_providerHealth = Meter.CreateGauge<int>(
			"dispatch.encryption.provider.health",
			"score",
			"Health score of encryption providers (0-100)");

		// Migration counter
		_fieldsMigrated = Meter.CreateCounter<long>(
			"dispatch.encryption.migration.fields_migrated",
			"count",
			"Number of fields migrated between encryption providers");

		// Key rotation counter
		_keyRotationsTotal = Meter.CreateCounter<long>(
			"dispatch.encryption.key_rotation.total",
			"count",
			"Total number of key rotations");

		// Bytes processed counter
		_bytesProcessed = Meter.CreateCounter<long>(
			"dispatch.encryption.bytes.processed",
			"bytes",
			"Total bytes processed by encryption operations");

		// Cache access counter
		_cacheAccesses = Meter.CreateCounter<long>(
			"dispatch.encryption.cache.accesses",
			"count",
			"Number of encryption key cache accesses");

		// Active keys gauge
		_activeKeys = Meter.CreateGauge<int>(
			"dispatch.encryption.keys.active",
			"count",
			"Number of active encryption keys");
	}

	/// <inheritdoc />
	public Meter Meter { get; }

	/// <inheritdoc />
	public void RecordOperation(string operation, string algorithm, string status, string provider)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(algorithm);
		ArgumentNullException.ThrowIfNull(status);
		ArgumentNullException.ThrowIfNull(provider);

		_operationsTotal.Add(1,
			new KeyValuePair<string, object?>("operation", operation),
			new KeyValuePair<string, object?>("algorithm", algorithm),
			new KeyValuePair<string, object?>("status", status),
			new KeyValuePair<string, object?>("provider", provider));
	}

	/// <inheritdoc />
	public void RecordOperationDuration(double durationMs, string operation, string provider)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(provider);

		_operationDuration.Record(durationMs,
			new KeyValuePair<string, object?>("operation", operation),
			new KeyValuePair<string, object?>("provider", provider));
	}

	/// <inheritdoc />
	public void UpdateProviderHealth(string provider, string status, int score)
	{
		ArgumentNullException.ThrowIfNull(provider);
		ArgumentNullException.ThrowIfNull(status);

		_providerHealth.Record(score,
			new KeyValuePair<string, object?>("provider", provider),
			new KeyValuePair<string, object?>("status", status));
	}

	/// <inheritdoc />
	public void RecordFieldsMigrated(long count, string fromProvider, string toProvider, string store)
	{
		ArgumentNullException.ThrowIfNull(fromProvider);
		ArgumentNullException.ThrowIfNull(toProvider);
		ArgumentNullException.ThrowIfNull(store);

		if (count > 0)
		{
			_fieldsMigrated.Add(count,
				new KeyValuePair<string, object?>("from_provider", fromProvider),
				new KeyValuePair<string, object?>("to_provider", toProvider),
				new KeyValuePair<string, object?>("store", store));
		}
	}

	/// <inheritdoc />
	public void RecordKeyRotation(string provider, string rotationType)
	{
		ArgumentNullException.ThrowIfNull(provider);
		ArgumentNullException.ThrowIfNull(rotationType);

		_keyRotationsTotal.Add(1,
			new KeyValuePair<string, object?>("provider", provider),
			new KeyValuePair<string, object?>("rotation_type", rotationType));
	}

	/// <inheritdoc />
	public void RecordBytesProcessed(long bytes, string operation, string provider)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(provider);

		if (bytes > 0)
		{
			_bytesProcessed.Add(bytes,
				new KeyValuePair<string, object?>("operation", operation),
				new KeyValuePair<string, object?>("provider", provider));
		}
	}

	/// <inheritdoc />
	public void RecordCacheAccess(bool hit, string provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		_cacheAccesses.Add(1,
			new KeyValuePair<string, object?>("hit", hit),
			new KeyValuePair<string, object?>("provider", provider));
	}

	/// <inheritdoc />
	public void UpdateActiveKeyCount(int count, string provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		_activeKeys.Record(count,
			new KeyValuePair<string, object?>("provider", provider));
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

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			Meter.Dispose();
			_disposed = true;
		}
	}
}
