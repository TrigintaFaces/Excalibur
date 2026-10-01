// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	/// Gets or sets how many of a stream's most recent appends stay recognisable as retries.
	/// </summary>
	/// <remarks>
	/// <para>
	/// An append reports success if and only if its events are durably present, decided by the identity of
	/// the events rather than by a version slot, so a retry after a lost acknowledgement is answered with
	/// the version the earlier attempt reached instead of a conflict that would invite a duplicating
	/// re-append. A Redis stream offers no keyed read by event identifier, so the store instead records the
	/// identity of each append in a companion sorted set and looks the retry up there. This is the size of
	/// that set.
	/// </para>
	/// <para>
	/// A retry of any of the last <em>N</em> appends to a stream is recognised, however many other writers
	/// appended in between. A retry older than that is reported as a concurrency conflict — the same answer
	/// a caller minting fresh identifiers per attempt receives, and the reason a caller must be idempotent
	/// downstream rather than relying on this window alone.
	/// </para>
	/// <para>
	/// The default covers far more intervening appends than a stream receives while one retry is in flight,
	/// while staying below the entry count at which Redis abandons the compact encoding for a sorted set, so
	/// the memory held per stream stays on the order of a kilobyte. Raise it for streams under unusual
	/// contention; lower it only if that per-stream cost matters more than the width of the window.
	/// </para>
	/// </remarks>
	/// <value>The number of recent appends per stream that remain recognisable. Defaults to 64.</value>
	[Range(1, 1024)]
	public int RetryRecognitionWindow { get; set; } = 64;

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
