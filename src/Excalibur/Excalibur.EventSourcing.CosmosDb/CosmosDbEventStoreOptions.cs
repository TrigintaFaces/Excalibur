// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.CosmosDb;

/// <summary>
/// Configuration options for the Cosmos DB event store.
/// </summary>
public sealed class CosmosDbEventStoreOptions
{
	/// <summary>
	/// Gets or sets the Cosmos DB database name that holds the events container.
	/// </summary>
	/// <value>Defaults to "events".</value>
	[Required]
	public string DatabaseName { get; set; } = "events";

	/// <summary>
	/// Gets or sets the events container name.
	/// </summary>
	/// <value>Defaults to "events".</value>
	[Required]
	public string EventsContainerName { get; set; } = "events";

	/// <summary>
	/// Gets or sets the partition key path.
	/// </summary>
	/// <value>Defaults to "/streamId".</value>
	[Required]
	public string PartitionKeyPath { get; set; } = "/streamId";

	/// <summary>
	/// Gets or sets the default time-to-live for events in seconds.
	/// </summary>
	/// <value>-1 for no expiration (default).</value>
	public int DefaultTimeToLiveSeconds { get; set; } = -1;

	/// <summary>
	/// Gets or sets a value indicating whether to use transactions for appending events.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool UseTransactionalBatch { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum batch size for change feed processing.
	/// </summary>
	/// <value>Defaults to 100.</value>
	[Range(1, int.MaxValue)]
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the poll interval for change feed in milliseconds.
	/// </summary>
	/// <value>Defaults to 1000ms.</value>
	[Range(1, int.MaxValue)]
	public int ChangeFeedPollIntervalMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to create the container if it doesn't exist.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool CreateContainerIfNotExists { get; set; } = true;

	/// <summary>
	/// Gets or sets the throughput for the events container (RU/s).
	/// </summary>
	/// <value>Defaults to 400 RU/s.</value>
	[Range(1, int.MaxValue)]
	public int ContainerThroughput { get; set; } = 400;

	/// <summary>
	/// Gets or sets a source-generated JSON type-info resolver covering the application's domain event types
	/// and the runtime types of the values it places in <see cref="Excalibur.Dispatch.IDomainEvent.Metadata"/>,
	/// enabling a reflection-free serialization path under trimming and native AOT.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Domain events are consumer types the framework cannot source-generate, so with no resolver the store
	/// serializes them through the reflection-based <see cref="System.Text.Json.JsonSerializer"/>. That works
	/// under the JIT, but a native-AOT application published with reflection-based serialization disabled has
	/// no reflection path to fall back on and the first append fails. Set this to a
	/// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> (or any
	/// <see cref="System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver"/>) covering those types to
	/// remove that dependency.
	/// </para>
	/// <para>
	/// The stored wire format does not vary with this setting. The resolver supplies type metadata only; the
	/// property naming policy, string-enum representation and null handling are the store's own and are
	/// applied to whichever resolver is in use, so events written with a resolver are byte-identical to events
	/// written without one and remain readable by a host configured either way.
	/// </para>
	/// <para>
	/// Metadata values are typed <see cref="object"/> and are therefore written as their runtime type. Declare
	/// each closed value type the application actually stores -- <c>string</c>, <c>int</c>, <c>bool</c> and so
	/// on. Do not declare <c>Dictionary&lt;string, object&gt;</c> as a shortcut: it compiles and then throws on
	/// the values it was meant to cover.
	/// </para>
	/// </remarks>
	/// <value>The consumer's event type-info resolver, or <see langword="null"/> to serialize through
	/// reflection.</value>
	public System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? EventTypeInfoResolver { get; set; }
}
