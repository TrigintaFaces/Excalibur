// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-infrastructure canonical-serializer lock for the event-store write path (jyp40l).
/// </summary>
/// <remarks>
/// <para>
/// The seam ruling: stores must serialize event payloads through the one canonical serializer
/// configuration used by the DI-registered <see cref="IEventSerializer"/> — an options instance
/// that includes a <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/> and
/// <see cref="System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull"/>. Per-store
/// inline <see cref="JsonSerializerOptions"/> (CamelCase only) diverge from that read path and
/// mis-encode enums (as numbers) and null-able members (emitted rather than omitted).
/// </para>
/// <para>
/// This lock binds the <b>persisted wire shape</b> the store actually writes — the observable seam,
/// independent of how the store is constructed — and the <b>DEFAULT</b> <see cref="JsonEventSerializer"/>
/// read path against those exact bytes. It runs against real Postgres (never skipped) per
/// <c>verify-against-real-infra-not-mock</c>. It is RED on the pre-fix inline-options impl:
/// the enum persists as a number and the null-able member persists as an explicit null.
/// </para>
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "EventStore")]
public sealed class PostgresEventStoreSerializerCanonicalizationShould : IClassFixture<PostgresEventStoreContainerFixture>
{
	private const string AggregateType = "SerializerCanonicalizationAggregate";
	private readonly PostgresEventStoreContainerFixture _fixture;

	public PostgresEventStoreSerializerCanonicalizationShould(PostgresEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	private async Task<PostgresEventStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"jyp40l canonical-serializer round-trip runs against real infrastructure and is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		return new PostgresEventStore(_fixture.ConnectionString, NullLogger<PostgresEventStore>.Instance);
	}

	private async Task<StoredEvent> AppendAndReloadAsync(PostgresEventStore store, SerializerLockEvent evt)
	{
		var result = await store.AppendAsync(
			evt.AggregateId, AggregateType, new[] { (IDomainEvent)evt }, expectedVersion: -1, CancellationToken.None);
		result.Success.ShouldBeTrue();

		var loaded = await store.LoadAsync(evt.AggregateId, AggregateType, CancellationToken.None);
		loaded.Count.ShouldBe(1);
		return loaded[0];
	}

	[Fact]
	public async Task Persist_an_enum_member_as_its_string_name_not_a_number()
	{
		var store = await CreateStoreAsync();
		var evt = new SerializerLockEvent
		{
			AggregateId = $"agg-{Guid.NewGuid():N}",
			Status = SerializerLockStatus.Active, // non-zero: distinguishes "Active" (string) from 1 (number)
		};

		var stored = await AppendAndReloadAsync(store, evt);

		using var doc = JsonParse(stored.EventData);
		var status = GetProperty(doc.RootElement, "status");
		status.ValueKind.ShouldBe(JsonValueKind.String,
			"the canonical serializer writes enums via JsonStringEnumConverter — the store must not persist a bare number");
		status.GetString().ShouldBe("Active",
			"the persisted enum must be the canonical string name, matching the DI IEventSerializer read path");
	}

	[Fact]
	public async Task Omit_a_null_member_from_the_persisted_payload()
	{
		var store = await CreateStoreAsync();
		var evt = new SerializerLockEvent
		{
			AggregateId = $"agg-{Guid.NewGuid():N}",
			Status = SerializerLockStatus.Pending,
			OptionalNote = null, // WhenWritingNull => property omitted entirely
		};

		var stored = await AppendAndReloadAsync(store, evt);

		using var doc = JsonParse(stored.EventData);
		doc.RootElement.TryGetProperty("optionalNote", out _).ShouldBeFalse(
			"the canonical serializer sets DefaultIgnoreCondition=WhenWritingNull — a null member must not be emitted");
	}

	[Fact]
	public async Task Round_trip_through_the_default_DI_serializer_read_path()
	{
		var store = await CreateStoreAsync();
		var evt = new SerializerLockEvent
		{
			AggregateId = $"agg-{Guid.NewGuid():N}",
			Status = SerializerLockStatus.Closed,
			OptionalNote = "kept",
		};

		var stored = await AppendAndReloadAsync(store, evt);

		// Bind the DEFAULT serializer (no hand-configured options) against the store's persisted bytes.
		var serializer = new JsonEventSerializer();
		var read = (SerializerLockEvent)serializer.DeserializeEvent(stored.EventData, typeof(SerializerLockEvent));

		read.Status.ShouldBe(SerializerLockStatus.Closed,
			"the enum written by the store must read back identically through the DEFAULT DI serializer");
		read.OptionalNote.ShouldBe("kept");
	}

	private static JsonDocument JsonParse(byte[] data) => JsonDocument.Parse(data);

	private static JsonElement GetProperty(JsonElement root, string name)
	{
		root.TryGetProperty(name, out var value).ShouldBeTrue($"persisted payload must contain '{name}'");
		return value;
	}
}

/// <summary>Status enum used to prove the store persists enums as canonical string names.</summary>
public enum SerializerLockStatus
{
	/// <summary>Zero value.</summary>
	Pending = 0,

	/// <summary>Non-zero value — distinguishes a string name from a numeric encoding.</summary>
	Active = 1,

	/// <summary>Terminal value.</summary>
	Closed = 2,
}

/// <summary>Event carrying an enum + a null-able member to bind the canonical serializer contract.</summary>
public sealed class SerializerLockEvent : IDomainEvent
{
	/// <inheritdoc/>
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <inheritdoc/>
	public string AggregateId { get; set; } = string.Empty;

	/// <inheritdoc/>
	public long Version { get; set; }

	/// <inheritdoc/>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	/// <inheritdoc/>
	public string EventType => nameof(SerializerLockEvent);

	/// <inheritdoc/>
	public IDictionary<string, object>? Metadata { get; set; }

	/// <summary>Gets or sets the status — an enum, to bind string-name persistence.</summary>
	public SerializerLockStatus Status { get; set; }

	/// <summary>Gets or sets an optional note — a null-able member, to bind WhenWritingNull.</summary>
	public string? OptionalNote { get; set; }
}
