// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Redis;

/// <summary>
/// Configuration options for the Redis event store.
/// </summary>
public sealed class RedisEventStoreOptions
{
	/// <summary>
	/// Gets or sets the Redis connection string.
	/// </summary>
	/// <value>The Redis connection string.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the key prefix for event streams.
	/// </summary>
	/// <value>The key prefix for event streams. Defaults to "es".</value>
	public string StreamKeyPrefix { get; set; } = "es";

	/// <summary>
	/// Gets or sets the Redis database index.
	/// </summary>
	/// <value>The database index. Defaults to -1 (default database).</value>
	[Range(-1, 15)]
	public int DatabaseIndex { get; set; } = -1;

	/// <summary>
	/// Gets or sets the default batch size for reading events from Redis streams.
	/// </summary>
	/// <value>The default batch size. Defaults to 100.</value>
	[Range(1, int.MaxValue)]
	public int DefaultBatchSize { get; set; } = 100;

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
