// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Abstractions.Observability;

/// <summary>
/// Standard telemetry helpers for write-side stores.
/// </summary>
public static class WriteStoreTelemetry
{
	/// <summary>
	/// The meter name for write-side store metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.WriteStores";

	/// <summary>
	/// The meter version for write-side store metrics.
	/// </summary>
	public const string MeterVersion = "1.0.0";

	// Static process-lifetime Meter: does not require disposal (same pattern as CdcTelemetryConstants.Meter).
	private static readonly Meter Meter = new(MeterName, MeterVersion);

	private static readonly Counter<long> OperationCounter = Meter.CreateCounter<long>(
		"dispatch.write_store.operations_total",
		unit: "operations",
		description: "Total number of write-side store operations");

	private static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
		"dispatch.write_store.operation_duration_ms",
		unit: "ms",
		description: "Duration of write-side store operations in milliseconds");

	/// <summary>
	/// Record a write-side store operation with standardized tags.
	/// </summary>
	/// <param name="store">The store type.</param>
	/// <param name="provider">The provider name.</param>
	/// <param name="operation">The operation name.</param>
	/// <param name="result">The result classification.</param>
	/// <param name="duration">The operation duration.</param>
	public static void RecordOperation(
		string store,
		string provider,
		string operation,
		string result,
		TimeSpan duration)
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(provider);
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(result);

		var tags = new TagList
		{
			{ Tags.Store, store },
			{ Tags.Provider, provider },
			{ Tags.Operation, operation },
			{ Tags.Result, result },
		};

		OperationCounter.Add(1, tags);
		OperationDuration.Record(duration.TotalMilliseconds, tags);
	}

	/// <summary>
	/// Starts a logging scope for write-side store operations.
	/// </summary>
	/// <param name="logger">The logger to create the scope on.</param>
	/// <param name="store">The store type.</param>
	/// <param name="provider">The provider name.</param>
	/// <param name="operation">The operation name.</param>
	/// <param name="messageId">The message identifier.</param>
	/// <param name="correlationId">The correlation identifier.</param>
	/// <param name="causationId">The causation identifier.</param>
	/// <returns>The logging scope.</returns>
	public static IDisposable? BeginLogScope(
		ILogger logger,
		string store,
		string provider,
		string? operation = null,
		string? messageId = null,
		string? correlationId = null,
		string? causationId = null)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(provider);

		var scope = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			[Tags.Store] = store,
			[Tags.Provider] = provider,
		};

		if (!string.IsNullOrWhiteSpace(operation))
		{
			scope[Tags.Operation] = operation;
		}

		if (!string.IsNullOrWhiteSpace(messageId))
		{
			scope[Tags.MessageId] = messageId;
		}

		if (!string.IsNullOrWhiteSpace(correlationId))
		{
			scope[Tags.CorrelationId] = correlationId;
		}

		if (!string.IsNullOrWhiteSpace(causationId))
		{
			scope[Tags.CausationId] = causationId;
		}

		var activity = Activity.Current;
		if (activity != null)
		{
			scope[Tags.TraceId] = activity.TraceId.ToString();
			scope[Tags.SpanId] = activity.SpanId.ToString();
		}

		return logger.BeginScope(scope);
	}

	/// <summary>
	/// Standard store type values.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible",
			Justification = "Nested groups keep write-store telemetry constants discoverable under a single entry point.")]
	public static class Stores
	{
		/// <summary>Event store operations.</summary>
		public const string EventStore = "event_store";

		/// <summary>Snapshot store operations.</summary>
		public const string SnapshotStore = "snapshot_store";

		/// <summary>Outbox store operations.</summary>
		public const string OutboxStore = "outbox_store";

		/// <summary>Inbox store operations.</summary>
		public const string InboxStore = "inbox_store";

		/// <summary>Saga state store operations.</summary>
		public const string SagaStore = "saga_store";

		/// <summary>Saga timeout store operations.</summary>
		public const string SagaTimeoutStore = "saga_timeout_store";

		/// <summary>CDC state store operations.</summary>
		public const string CdcStateStore = "cdc_state_store";

		/// <summary>Dead letter store operations.</summary>
		public const string DeadLetterStore = "dead_letter_store";
	}

	/// <summary>
	/// Standard provider values.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible",
			Justification = "Nested groups keep write-store telemetry constants discoverable under a single entry point.")]
	public static class Providers
	{
		/// <summary>SQL Server provider.</summary>
		public const string SqlServer = "sqlserver";

		/// <summary>Postgres provider.</summary>
		public const string Postgres = "postgres";

		/// <summary>MongoDB provider.</summary>
		public const string MongoDb = "mongodb";

		/// <summary>Cosmos DB provider.</summary>
		public const string CosmosDb = "cosmosdb";

		/// <summary>DynamoDB provider.</summary>
		public const string DynamoDb = "dynamodb";

		/// <summary>Firestore provider.</summary>
		public const string Firestore = "firestore";

		/// <summary>Redis provider.</summary>
		public const string Redis = "redis";

		/// <summary>In-memory provider.</summary>
		public const string InMemory = "inmemory";
	}

	/// <summary>
	/// Standard result values.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible",
			Justification = "Nested groups keep write-store telemetry constants discoverable under a single entry point.")]
	public static class Results
	{
		/// <summary>Operation succeeded.</summary>
		public const string Success = "success";

		/// <summary>Operation failed.</summary>
		public const string Failure = "failure";

		/// <summary>Operation failed due to a concurrency conflict.</summary>
		public const string Conflict = "conflict";

		/// <summary>Operation result was not found.</summary>
		public const string NotFound = "not_found";
	}

	/// <summary>
	/// Standard tag keys for write-side store telemetry.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible",
			Justification = "Nested groups keep write-store telemetry constants discoverable under a single entry point.")]
	public static class Tags
	{
		/// <summary>The store type tag.</summary>
		public const string Store = "store";

		/// <summary>The provider name tag.</summary>
		public const string Provider = "provider";

		/// <summary>The operation name tag.</summary>
		public const string Operation = "operation";

		/// <summary>The operation result tag.</summary>
		public const string Result = "result";

		/// <summary>The correlation identifier tag.</summary>
		public const string CorrelationId = "correlation.id";

		/// <summary>The causation identifier tag.</summary>
		public const string CausationId = "causation.id";

		/// <summary>The message identifier tag.</summary>
		public const string MessageId = "message.id";

		/// <summary>The trace identifier tag.</summary>
		public const string TraceId = "trace.id";

		/// <summary>The span identifier tag.</summary>
		public const string SpanId = "span.id";
	}
}
