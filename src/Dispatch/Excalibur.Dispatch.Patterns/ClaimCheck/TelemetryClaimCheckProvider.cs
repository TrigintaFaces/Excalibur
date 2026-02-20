// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Decorates an <see cref="IClaimCheckProvider"/> with OpenTelemetry metrics and distributed tracing.
/// Records store/retrieve/delete counters, duration histograms, and payload size histograms.
/// </summary>
public sealed class TelemetryClaimCheckProvider : IClaimCheckProvider
{
	private readonly IClaimCheckProvider _inner;
	private readonly Counter<long> _storedCounter;
	private readonly Counter<long> _retrievedCounter;
	private readonly Counter<long> _deletedCounter;
	private readonly Counter<long> _failedCounter;
	private readonly Histogram<double> _storeDurationHistogram;
	private readonly Histogram<double> _retrieveDurationHistogram;
	private readonly Histogram<long> _payloadSizeHistogram;
	private readonly ActivitySource _activitySource;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetryClaimCheckProvider"/> class.
	/// </summary>
	/// <param name="inner">The inner claim check provider to decorate.</param>
	/// <param name="meter">The meter for recording metrics.</param>
	/// <param name="activitySource">The activity source for distributed tracing.</param>
	public TelemetryClaimCheckProvider(
		IClaimCheckProvider inner,
		Meter meter,
		ActivitySource activitySource)
	{
		ArgumentNullException.ThrowIfNull(inner);
		ArgumentNullException.ThrowIfNull(meter);
		_inner = inner;
		_activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));

		_storedCounter = meter.CreateCounter<long>(
			ClaimCheckTelemetryConstants.MetricNames.PayloadsStored,
			"payloads",
			"Total payloads stored successfully");

		_retrievedCounter = meter.CreateCounter<long>(
			ClaimCheckTelemetryConstants.MetricNames.PayloadsRetrieved,
			"payloads",
			"Total payloads retrieved successfully");

		_deletedCounter = meter.CreateCounter<long>(
			ClaimCheckTelemetryConstants.MetricNames.PayloadsDeleted,
			"payloads",
			"Total payloads deleted successfully");

		_failedCounter = meter.CreateCounter<long>(
			ClaimCheckTelemetryConstants.MetricNames.OperationsFailed,
			"operations",
			"Total operation failures");

		_storeDurationHistogram = meter.CreateHistogram<double>(
			ClaimCheckTelemetryConstants.MetricNames.StoreDuration,
			"ms",
			"Duration of store operations in milliseconds");

		_retrieveDurationHistogram = meter.CreateHistogram<double>(
			ClaimCheckTelemetryConstants.MetricNames.RetrieveDuration,
			"ms",
			"Duration of retrieve operations in milliseconds");

		_payloadSizeHistogram = meter.CreateHistogram<long>(
			ClaimCheckTelemetryConstants.MetricNames.PayloadSize,
			"By",
			"Payload size in bytes");
	}

	/// <inheritdoc />
	public async Task<ClaimCheckReference> StoreAsync(
		byte[] payload,
		CancellationToken cancellationToken,
		ClaimCheckMetadata? metadata = null)
	{
		using var activity = _activitySource.StartActivity("claimcheck.store");
		activity?.SetTag(ClaimCheckTelemetryConstants.Tags.Operation, "store");

		var stopwatch = Stopwatch.StartNew();
		try
		{
			var reference = await _inner.StoreAsync(payload, cancellationToken, metadata).ConfigureAwait(false);
			stopwatch.Stop();

			var tags = new TagList
			{
				{ ClaimCheckTelemetryConstants.Tags.Operation, "store" },
			};
			_storedCounter.Add(1, tags);
			_storeDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
			_payloadSizeHistogram.Record(payload.Length, tags);

			return reference;
		}
		catch (Exception ex)
		{
			stopwatch.Stop();

			var failTags = new TagList
			{
				{ ClaimCheckTelemetryConstants.Tags.Operation, "store" },
				{ ClaimCheckTelemetryConstants.Tags.ErrorType, ex.GetType().Name },
			};
			_failedCounter.Add(1, failTags);
			_storeDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, failTags);

			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<byte[]> RetrieveAsync(
		ClaimCheckReference reference,
		CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("claimcheck.retrieve");
		activity?.SetTag(ClaimCheckTelemetryConstants.Tags.Operation, "retrieve");

		var stopwatch = Stopwatch.StartNew();
		try
		{
			var result = await _inner.RetrieveAsync(reference, cancellationToken).ConfigureAwait(false);
			stopwatch.Stop();

			var tags = new TagList
			{
				{ ClaimCheckTelemetryConstants.Tags.Operation, "retrieve" },
			};
			_retrievedCounter.Add(1, tags);
			_retrieveDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
			_payloadSizeHistogram.Record(result.Length, tags);

			return result;
		}
		catch (Exception ex)
		{
			stopwatch.Stop();

			var failTags = new TagList
			{
				{ ClaimCheckTelemetryConstants.Tags.Operation, "retrieve" },
				{ ClaimCheckTelemetryConstants.Tags.ErrorType, ex.GetType().Name },
			};
			_failedCounter.Add(1, failTags);
			_retrieveDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, failTags);

			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAsync(
		ClaimCheckReference reference,
		CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("claimcheck.delete");
		activity?.SetTag(ClaimCheckTelemetryConstants.Tags.Operation, "delete");

		try
		{
			var result = await _inner.DeleteAsync(reference, cancellationToken).ConfigureAwait(false);

			if (result)
			{
				var tags = new TagList
				{
					{ ClaimCheckTelemetryConstants.Tags.Operation, "delete" },
				};
				_deletedCounter.Add(1, tags);
			}

			return result;
		}
		catch (Exception ex)
		{
			var failTags = new TagList
			{
				{ ClaimCheckTelemetryConstants.Tags.Operation, "delete" },
				{ ClaimCheckTelemetryConstants.Tags.ErrorType, ex.GetType().Name },
			};
			_failedCounter.Add(1, failTags);

			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			throw;
		}
	}

	/// <inheritdoc />
	public bool ShouldUseClaimCheck(byte[] payload) => _inner.ShouldUseClaimCheck(payload);
}
