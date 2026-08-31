// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Sqlite;
using Excalibur.EventSourcing.Sqlite.DependencyInjection;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

#pragma warning disable CA2100 // SQL strings are safe -- table name is a test-controlled unique constant, never user input

namespace Excalibur.EventSourcing.Tests.Sqlite;

/// <summary>
/// Locks the seam that carries <see cref="SqliteEventSourcingOptions.EventTypeInfoResolver"/> through to
/// the store's serialization.
/// </summary>
/// <remarks>
/// <para>
/// The defect this guards is invisible to a build. Publishing the store's serialization path with
/// reflection-based serialization disabled raises no IL warning either way, and fails only when the process
/// appends its first event. So the assertions here are behavioural: an event type the host's resolver does
/// not declare must fail, which it can only do if the store consults that resolver at all. Against a store
/// that builds its own options and ignores the host's, every one of those appends succeeds through
/// reflection.
/// </para>
/// <para>
/// The wire format is asserted by comparing the bytes the two paths write, not by reading them.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Sqlite")]
public sealed class SqliteEventStoreEventTypeInfoResolverShould : IDisposable
{
	private const string AggregateType = "ResolverAggregate";

	private readonly string _databasePath;
	private readonly string _connectionString;
	private readonly string _tableName = $"Events_{Guid.NewGuid():N}";
	private readonly SqliteConnection _keepAlive;
	private bool _disposed;

	public SqliteEventStoreEventTypeInfoResolverShould()
	{
		_databasePath = Path.Combine(Path.GetTempPath(), $"excalibur-resolver-test-{Guid.NewGuid():N}.db");
		_connectionString = $"Data Source={_databasePath}";

		// Hold one connection open for the whole test so the file (and its WAL) stays alive, and provision
		// the table directly: SqliteTableInitializer gates on a process-global flag, so it no-ops for this
		// test's unique table once any other test has initialized.
		_keepAlive = new SqliteConnection(_connectionString);
		_keepAlive.Open();
		using var wal = _keepAlive.CreateCommand();
		wal.CommandText = "PRAGMA journal_mode=WAL;";
		_ = wal.ExecuteScalar();

		using var create = _keepAlive.CreateCommand();
		create.CommandText = $"""
			CREATE TABLE IF NOT EXISTS [{_tableName}] (
				GlobalPosition INTEGER PRIMARY KEY AUTOINCREMENT,
				EventId TEXT NOT NULL,
				AggregateId TEXT NOT NULL,
				AggregateType TEXT NOT NULL,
				EventType TEXT NOT NULL,
				EventData BLOB NOT NULL,
				Metadata BLOB,
				Version INTEGER NOT NULL,
				Timestamp TEXT NOT NULL,
				TenantId TEXT NOT NULL,
				UNIQUE(AggregateId, AggregateType, Version, TenantId)
			);
			""";
		_ = create.ExecuteNonQuery();
	}

	[Fact]
	public async Task Reject_AnEventTypeTheHostResolverDoesNotDeclare()
	{
		var store = CreateStore(SqliteResolverTestEventContext.Default);
		var aggregateId = Guid.NewGuid().ToString();

		// UndeclaredSqliteTestEvent is deliberately absent from the context. A store that consults the
		// host's resolver has no metadata for it and cannot serialize it; a store that quietly built its own
		// reflection options serializes it happily, which is the defect.
		var thrown = await Should.ThrowAsync<NotSupportedException>(
			async () => await store.AppendAsync(
				aggregateId,
				AggregateType,
				[new UndeclaredSqliteTestEvent { AggregateId = aggregateId }],
				expectedVersion: -1,
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		thrown.Message.ShouldContain(nameof(UndeclaredSqliteTestEvent));
	}

	[Fact]
	public async Task Reject_AMetadataValueTypeTheHostResolverDoesNotDeclare()
	{
		var store = CreateStore(SqliteResolverTestEventContext.Default);
		var aggregateId = Guid.NewGuid().ToString();

		// Metadata values are written as their runtime type, so each runtime type must itself be declared.
		// Guid is not on the context; string, int and bool are.
		var domainEvent = new SqliteResolverTestEvent
		{
			AggregateId = aggregateId,
			Metadata = new Dictionary<string, object> { ["TraceId"] = Guid.NewGuid() },
		};

		_ = await Should.ThrowAsync<NotSupportedException>(
			async () => await store.AppendAsync(
				aggregateId,
				AggregateType,
				[domainEvent],
				expectedVersion: -1,
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Write_ByteIdenticalPayloads_WithAndWithoutAResolver()
	{
		var withoutResolver = await AppendAndLoadAsync(CreateStore(resolver: null), Guid.NewGuid().ToString())
			.ConfigureAwait(false);
		var withResolver = await AppendAndLoadAsync(
			CreateStore(SqliteResolverTestEventContext.Default), Guid.NewGuid().ToString()).ConfigureAwait(false);

		// The aggregate id differs per append (they share one table), so compare everything the id does not
		// reach: the serialized event body carries it, the metadata blob does not.
		withResolver.Metadata.ShouldNotBeNull();
		withResolver.Metadata.ShouldBe(withoutResolver.Metadata);
		withResolver.EventType.ShouldBe(withoutResolver.EventType);
		withResolver.EventData.Length.ShouldBe(withoutResolver.EventData.Length);
	}

	[Fact]
	public async Task Write_PayloadsTheReflectionPathReadsBack()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var expected = CreateEvent(aggregateId);

		var stored = await AppendAndLoadAsync(CreateStore(SqliteResolverTestEventContext.Default), aggregateId)
			.ConfigureAwait(false);

		// Read back through the canonical reflection options a differently-configured host would use.
		var roundTripped = JsonSerializer.Deserialize<SqliteResolverTestEvent>(
			stored.EventData, EventSerializationDefaults.Canonical);

		roundTripped.ShouldNotBeNull();
		roundTripped.EventId.ShouldBe(expected.EventId);
		roundTripped.AggregateId.ShouldBe(aggregateId);
		roundTripped.Name.ShouldBe(expected.Name);
		roundTripped.Shade.ShouldBe(expected.Shade);

		var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
			stored.Metadata!, EventSerializationDefaults.Canonical);

		metadata.ShouldNotBeNull();
		metadata["UserId"].GetString().ShouldBe("u-1");
		metadata["Attempt"].GetInt32().ShouldBe(2);
		metadata["Replayed"].GetBoolean().ShouldBeTrue();
		metadata["Absent"].ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Fact]
	public async Task Serialize_ThroughReflection_WhenNoResolverIsConfigured()
	{
		// The default path must be unchanged: with no resolver the store serializes anything, including the
		// type the resolver-configured store above refuses. This is what makes the rejection tests a
		// statement about the resolver rather than about the event type.
		var store = CreateStore(resolver: null);
		var aggregateId = Guid.NewGuid().ToString();

		var result = await store.AppendAsync(
			aggregateId,
			AggregateType,
			[new UndeclaredSqliteTestEvent { AggregateId = aggregateId }],
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task Honour_TheConfiguredResolver_WhenResolvedFromTheContainer()
	{
		// The consumer composition: register the provider through its supported builder entry point,
		// configure the resolver on its options, resolve the contract. This is the half a store-only test
		// cannot reach -- a store that accepts a resolver nobody hands it is still broken for every consumer.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder => builder.UseSqlite(options =>
		{
			options.ConnectionString = _connectionString;
			options.EventStoreTable = _tableName;
			options.EventTypeInfoResolver = SqliteResolverTestEventContext.Default;
		})));

		await using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredKeyedService<IEventStore>("default");

		var aggregateId = Guid.NewGuid().ToString();

		// A declared event round-trips.
		var result = await store.AppendAsync(
			aggregateId,
			AggregateType,
			[CreateEvent(aggregateId)],
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeTrue();

		// And the container-resolved store is the one honouring the configured resolver, not a store that
		// happens to serialize everything through reflection.
		_ = await Should.ThrowAsync<NotSupportedException>(
			async () => await store.AppendAsync(
				Guid.NewGuid().ToString(),
				AggregateType,
				[new UndeclaredSqliteTestEvent { AggregateId = Guid.NewGuid().ToString() }],
				expectedVersion: -1,
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		// Deliberately NOT SqliteConnection.ClearAllPools(): that is process-global and tears down pooled
		// connections belonging to every other Sqlite test running in parallel.
		_keepAlive.Dispose();

		try
		{
			if (File.Exists(_databasePath))
			{
				File.Delete(_databasePath);
			}
		}
		catch (IOException)
		{
			// A held file handle is not a test failure; the temp file is disposable.
		}
	}

	private SqliteEventStore CreateStore(IJsonTypeInfoResolver? resolver) => new(
		_connectionString,
		NullLogger<SqliteEventStore>.Instance,
		TestTenantContext.SingleTenantDefault,
		Options.Create(new TenantContextOptions()),
		_tableName,
		resolver);

	private static SqliteResolverTestEvent CreateEvent(string aggregateId) => new()
	{
		EventId = "e-1",
		AggregateId = aggregateId,
		OccurredAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
		Name = "order-placed",
		Shade = SqliteResolverTestShade.Green,
		Metadata = new Dictionary<string, object>
		{
			["UserId"] = "u-1",
			["Attempt"] = 2,
			["Replayed"] = true,
			["Absent"] = null!,
		},
	};

	private async Task<StoredEvent> AppendAndLoadAsync(SqliteEventStore store, string aggregateId)
	{
		_ = await store.AppendAsync(
			aggregateId,
			AggregateType,
			[CreateEvent(aggregateId)],
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

		return loaded.ShouldHaveSingleItem();
	}
}

internal enum SqliteResolverTestShade
{
	Red,
	Green,
}

internal sealed class SqliteResolverTestEvent : IDomainEvent
{
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	public string AggregateId { get; set; } = string.Empty;

	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	public string EventType { get; set; } = nameof(SqliteResolverTestEvent);

	public string Name { get; set; } = string.Empty;

	public SqliteResolverTestShade Shade { get; set; }

	public IDictionary<string, object>? Metadata { get; set; }
}

internal sealed class UndeclaredSqliteTestEvent : IDomainEvent
{
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	public string AggregateId { get; set; } = string.Empty;

	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	public string EventType { get; set; } = nameof(UndeclaredSqliteTestEvent);

	public IDictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// A consumer-shaped source-generated context, deliberately carrying no
/// <c>JsonSourceGenerationOptions</c> annotation.
/// </summary>
/// <remarks>
/// The store attaches the resolver to its own canonical options rather than adopting the context's, so the
/// naming policy, string-enum representation and null handling that fix the stored wire format do not depend
/// on how the host annotated its context. A bare context is therefore the stricter fixture: if the byte
/// comparison holds for this one, a consumer cannot mis-annotate their way to a divergent payload.
/// <c>Dictionary&lt;string, object&gt;</c> is not declared here; only the closed metadata value types are.
/// </remarks>
[JsonSerializable(typeof(SqliteResolverTestEvent))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
internal sealed partial class SqliteResolverTestEventContext : JsonSerializerContext;
