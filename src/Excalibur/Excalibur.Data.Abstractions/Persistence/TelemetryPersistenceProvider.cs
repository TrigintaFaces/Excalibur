// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Decorator that instruments all persistence operations with OpenTelemetry
/// <see cref="ActivitySource"/> spans and <see cref="Meter"/> counters.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <c>TelemetryEventStore</c> / <c>TelemetrySnapshotStore</c> pattern
/// from S553. Uses <see cref="PersistenceTelemetryConstants"/> for shared telemetry.
/// </para>
/// <para>
/// Compose via <see cref="PersistenceProviderBuilder"/>:
/// <code>
/// builder.Use(inner => new TelemetryPersistenceProvider(inner));
/// </code>
/// </para>
/// </remarks>
public sealed class TelemetryPersistenceProvider : DelegatingPersistenceProvider
{
	private static readonly Counter<long> ExecuteCounter =
		PersistenceTelemetryConstants.Meter.CreateCounter<long>(
			"persistence.execute.count",
			"operations",
			"Number of persistence execute operations");

	private static readonly Counter<long> ExecuteErrorCounter =
		PersistenceTelemetryConstants.Meter.CreateCounter<long>(
			"persistence.execute.errors",
			"errors",
			"Number of persistence execute errors");

	private static readonly Histogram<double> ExecuteDuration =
		PersistenceTelemetryConstants.Meter.CreateHistogram<double>(
			"persistence.execute.duration",
			"ms",
			"Duration of persistence execute operations in milliseconds");

	private static readonly Counter<long> InitializeCounter =
		PersistenceTelemetryConstants.Meter.CreateCounter<long>(
			"persistence.initialize.count",
			"operations",
			"Number of persistence initialize operations");

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetryPersistenceProvider"/> class.
	/// </summary>
	/// <param name="innerProvider">The inner provider to decorate.</param>
	public TelemetryPersistenceProvider(IPersistenceProvider innerProvider)
		: base(innerProvider)
	{
	}

	/// <inheritdoc />
	public override async Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
	{
		var requestType = request.GetType().Name;

		using var activity = PersistenceTelemetryConstants.ActivitySource.StartActivity(
			$"Persistence.Execute {requestType}",
			ActivityKind.Client);

		activity?.SetTag(PersistenceTelemetryConstants.AttributeProviderName, Name);
		activity?.SetTag(PersistenceTelemetryConstants.AttributeProviderType, ProviderType);
		activity?.SetTag(PersistenceTelemetryConstants.AttributeOperation, "Execute");
		activity?.SetTag(PersistenceTelemetryConstants.AttributeRequestType, requestType);

		var sw = ValueStopwatch.StartNew();
		try
		{
			var result = await base.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

			ExecuteCounter.Add(1,
				new KeyValuePair<string, object?>(PersistenceTelemetryConstants.AttributeProviderName, Name),
				new KeyValuePair<string, object?>(PersistenceTelemetryConstants.AttributeRequestType, requestType));
			ExecuteDuration.Record(sw.Elapsed.TotalMilliseconds,
				new KeyValuePair<string, object?>(PersistenceTelemetryConstants.AttributeProviderName, Name),
				new KeyValuePair<string, object?>(PersistenceTelemetryConstants.AttributeRequestType, requestType));

			activity?.SetStatus(ActivityStatusCode.Ok);
			return result;
		}
		catch (Exception ex)
		{
			ExecuteErrorCounter.Add(1,
				new KeyValuePair<string, object?>(PersistenceTelemetryConstants.AttributeProviderName, Name),
				new KeyValuePair<string, object?>(PersistenceTelemetryConstants.AttributeRequestType, requestType));
			ExecuteDuration.Record(sw.Elapsed.TotalMilliseconds,
				new KeyValuePair<string, object?>(PersistenceTelemetryConstants.AttributeProviderName, Name),
				new KeyValuePair<string, object?>(PersistenceTelemetryConstants.AttributeRequestType, requestType));

			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.AddTag("exception.type", ex.GetType().FullName);
			activity?.AddTag("exception.message", ex.Message);
			throw;
		}
	}

	/// <inheritdoc />
	public override async Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
	{
		using var activity = PersistenceTelemetryConstants.ActivitySource.StartActivity(
			"Persistence.Initialize",
			ActivityKind.Client);

		activity?.SetTag(PersistenceTelemetryConstants.AttributeProviderName, Name);
		activity?.SetTag(PersistenceTelemetryConstants.AttributeProviderType, ProviderType);
		activity?.SetTag(PersistenceTelemetryConstants.AttributeOperation, "Initialize");

		try
		{
			await base.InitializeAsync(options, cancellationToken).ConfigureAwait(false);

			InitializeCounter.Add(1,
				new KeyValuePair<string, object?>(PersistenceTelemetryConstants.AttributeProviderName, Name));

			activity?.SetStatus(ActivityStatusCode.Ok);
		}
		catch (Exception ex)
		{
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.AddTag("exception.type", ex.GetType().FullName);
			activity?.AddTag("exception.message", ex.Message);
			throw;
		}
	}
}
