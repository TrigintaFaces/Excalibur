// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Data.IdentityMap.Diagnostics;

/// <summary>
/// Telemetry decorator for <see cref="IIdentityMapStore"/> that instruments operations
/// with counters and histograms.
/// </summary>
internal sealed class TelemetryIdentityMapStoreDecorator : IIdentityMapStore, IDisposable
{
	/// <summary>
	/// The meter name for identity map store telemetry.
	/// </summary>
	public const string MeterName = "Excalibur.Data.IdentityMap";

	private readonly IIdentityMapStore _inner;
	private readonly Meter _meter;
	private readonly Counter<long> _operationsCounter;
	private readonly Histogram<double> _operationDuration;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetryIdentityMapStoreDecorator"/> class.
	/// </summary>
	/// <param name="inner">The inner identity map store to decorate.</param>
	/// <param name="meterFactory">The meter factory for creating instruments.</param>
	public TelemetryIdentityMapStoreDecorator(IIdentityMapStore inner, IMeterFactory? meterFactory = null)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);

		_operationsCounter = _meter.CreateCounter<long>(
			"excalibur.identitymap.operations",
			description: "Number of identity map store operations.");

		_operationDuration = _meter.CreateHistogram<double>(
			"excalibur.identitymap.operation_duration",
			unit: "ms",
			description: "Duration of identity map store operations in milliseconds.");
	}

	/// <inheritdoc/>
	public async ValueTask<string?> ResolveAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await _inner.ResolveAsync(externalSystem, externalId, aggregateType, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("resolve", externalSystem, aggregateType, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask BindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		string aggregateId,
		CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			await _inner.BindAsync(externalSystem, externalId, aggregateType, aggregateId, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("bind", externalSystem, aggregateType, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<IdentityBindResult> TryBindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		string aggregateId,
		CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await _inner.TryBindAsync(externalSystem, externalId, aggregateType, aggregateId, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("try_bind", externalSystem, aggregateType, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> UnbindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await _inner.UnbindAsync(externalSystem, externalId, aggregateType, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("unbind", externalSystem, aggregateType, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyDictionary<string, string>> ResolveBatchAsync(
		string externalSystem,
		IReadOnlyList<string> externalIds,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await _inner.ResolveBatchAsync(externalSystem, externalIds, aggregateType, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("resolve_batch", externalSystem, aggregateType, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_meter.Dispose();
	}

	private void RecordOperation(string operation, string externalSystem, string aggregateType, double durationMs)
	{
		var tags = new TagList
		{
			{ "operation", operation },
			{ "external_system", externalSystem },
			{ "aggregate_type", aggregateType },
		};

		_operationsCounter.Add(1, tags);
		_operationDuration.Record(durationMs, tags);
	}
}
