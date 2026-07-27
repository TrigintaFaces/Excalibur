// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.Diagnostics;

/// <summary>
/// Telemetry decorator for <see cref="ISagaStore"/> that instruments operations
/// with counters and histograms.
/// </summary>
internal sealed class TelemetrySagaStoreDecorator : ISagaStore, IDisposable
{
	/// <summary>
	/// The meter name for saga store telemetry.
	/// </summary>
	public const string MeterName = "Excalibur.Saga";

	private readonly ISagaStore _inner;
	private readonly Meter _meter;
	private readonly Counter<long> _operationsCounter;
	private readonly Histogram<double> _operationDuration;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetrySagaStoreDecorator"/> class.
	/// </summary>
	/// <param name="inner">The inner saga store to decorate.</param>
	/// <param name="meterFactory">The meter factory for creating instruments.</param>
	public TelemetrySagaStoreDecorator(ISagaStore inner, IMeterFactory? meterFactory = null)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);

		_operationsCounter = _meter.CreateCounter<long>(
			"excalibur.saga.operations",
			description: "Number of saga store operations.");

		_operationDuration = _meter.CreateHistogram<double>(
			"excalibur.saga.operation_duration",
			unit: "ms",
			description: "Duration of saga store operations in milliseconds.");
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await _inner.LoadAsync<TSagaState>(sagaId, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("load", typeof(TSagaState).Name, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			await _inner.SaveAsync(sagaState, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("save", typeof(TSagaState).Name, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await _inner.PurgeCompletedBeforeAsync(threshold, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("purge", "(all)", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Forwards to the inner store. This override is <b>required</b>, not decorative: the interface member has a
	/// throwing default implementation, so a decorator that did not override it would report the capability as
	/// unsupported even when the store it wraps supports it — the decorator would silently disable estate-wide
	/// retention just by being installed. The distinct operation name keeps the scoped and estate-wide sweeps
	/// separable in metrics, which is the point of naming them differently in the first place.
	/// </remarks>
	public async Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await _inner.PurgeAllTenantsCompletedBeforeAsync(threshold, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("purge_all_tenants", "(all)", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_meter.Dispose();
	}

	private void RecordOperation(string operation, string sagaType, double durationMs)
	{
		var tags = new TagList
		{
			{ "operation", operation },
			{ "saga_type", sagaType },
		};

		_operationsCounter.Add(1, tags);
		_operationDuration.Record(durationMs, tags);
	}
}
