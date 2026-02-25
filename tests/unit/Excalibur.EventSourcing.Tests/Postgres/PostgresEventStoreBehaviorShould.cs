// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Tests.Postgres;

[Trait("Category", "Unit")]
public sealed class PostgresEventStoreBehaviorShould : UnitTestBase
{
	private const string InvalidConnectionString =
		"Host=127.0.0.1;Port=1;Database=events;Username=postgres;Password=invalid;Timeout=1;Command Timeout=1;Pooling=false";

	private static readonly ILogger<PostgresEventStore> Logger = NullLoggerFactory.CreateLogger<PostgresEventStore>();

	[Fact]
	public async Task AppendAsync_ReturnSuccess_WhenNoEvents()
	{
		var sut = new PostgresEventStore(InvalidConnectionString, Logger);

		var result = await sut.AppendAsync("agg-1", "Order", [], expectedVersion: 5, CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(5);
		result.FirstEventPosition.ShouldBe(0);
	}

	[Fact]
	public async Task AppendAsync_ReturnFailureResult_WhenConnectionCannotBeOpened()
	{
		var sut = new PostgresEventStore(InvalidConnectionString, Logger);
		var events = new IDomainEvent[] { new TestDomainEvent("evt-1") };

		var result = await sut.AppendAsync("agg-1", "Order", events, expectedVersion: 0, CancellationToken.None);

		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task LoadAsync_Throw_WhenConnectionCannotBeOpened()
	{
		var sut = new PostgresEventStore(InvalidConnectionString, Logger);

		await Should.ThrowAsync<Exception>(() =>
			sut.LoadAsync("agg-1", "Order", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task LoadAsync_FromVersion_Throw_WhenConnectionCannotBeOpened()
	{
		var sut = new PostgresEventStore(InvalidConnectionString, Logger);

		await Should.ThrowAsync<Exception>(() =>
			sut.LoadAsync("agg-1", "Order", 10, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_Throw_WhenConnectionCannotBeOpened()
	{
		var sut = new PostgresEventStore(InvalidConnectionString, Logger);

		await Should.ThrowAsync<Exception>(() =>
			sut.GetUndispatchedEventsAsync(100, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_Throw_WhenConnectionCannotBeOpened()
	{
		var sut = new PostgresEventStore(InvalidConnectionString, Logger);

		await Should.ThrowAsync<Exception>(() =>
			sut.MarkEventAsDispatchedAsync("evt-1", CancellationToken.None).AsTask());
	}

	[Fact]
	public void SerializeEvent_UsePayloadSerializer_WhenProvided()
	{
		var payloadSerializer = new StubPayloadSerializer([7, 8, 9]);
		var sut = new PostgresEventStore(
			InvalidConnectionString,
			Logger,
			internalSerializer: null,
			payloadSerializer: payloadSerializer);
		var method = typeof(PostgresEventStore).GetMethod("SerializeEvent", BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var bytes = (byte[])method!.Invoke(sut, [new TestDomainEvent("evt-1")])!;

		bytes.ShouldBe([7, 8, 9]);
		payloadSerializer.SerializeCallCount.ShouldBe(1);
	}

	[Fact]
	public void SerializeMetadata_ReturnJsonPayload()
	{
		var sut = new PostgresEventStore(InvalidConnectionString, Logger);
		var method = typeof(PostgresEventStore).GetMethod("SerializeMetadata", BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var payload = (byte[])method!.Invoke(sut, [new Dictionary<string, object> { ["k"] = "v" }])!;
		payload.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void SerializeEventWithEnvelopeSupport_PrependFormatMarker_WhenInternalSerializerConfigured()
	{
		var payloadSerializer = new StubPayloadSerializer([1, 2, 3, 4]);
		var internalSerializer = new StubInternalSerializer([9, 10, 11]);
		var sut = new PostgresEventStore(
			InvalidConnectionString,
			Logger,
			internalSerializer: internalSerializer,
			payloadSerializer: payloadSerializer);
		var method = typeof(PostgresEventStore).GetMethod(
			"SerializeEventWithEnvelopeSupport",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var bytes = (byte[])method!.Invoke(sut, [new TestDomainEvent("evt-2"), "agg-1", "Order", 2L])!;

		bytes.Length.ShouldBe(4);
		bytes[0].ShouldBe((byte)0x01);
		bytes[1].ShouldBe((byte)9);
		bytes[2].ShouldBe((byte)10);
		bytes[3].ShouldBe((byte)11);
	}

	[Fact]
	public void SerializeEventWithEnvelopeSupport_FallbackToPayloadSerializer_WhenInternalSerializerMissing()
	{
		var payloadSerializer = new StubPayloadSerializer([5, 6, 7]);
		var sut = new PostgresEventStore(
			InvalidConnectionString,
			Logger,
			internalSerializer: null,
			payloadSerializer: payloadSerializer);
		var method = typeof(PostgresEventStore).GetMethod(
			"SerializeEventWithEnvelopeSupport",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var bytes = (byte[])method!.Invoke(sut, [new TestDomainEvent("evt-3"), "agg-1", "Order", 3L])!;

		bytes.ShouldBe([5, 6, 7]);
		payloadSerializer.SerializeCallCount.ShouldBe(1);
	}

	[Fact]
	public void GetFullExceptionMessage_FlattenInnerExceptionChain()
	{
		var method = typeof(PostgresEventStore).GetMethod("GetFullExceptionMessage", BindingFlags.Static | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var ex = new InvalidOperationException("outer", new Exception("middle", new Exception("inner")));
		var message = (string)method!.Invoke(null, [ex])!;

		message.ShouldContain("outer");
		message.ShouldContain("middle");
		message.ShouldContain("inner");
		message.ShouldContain(" -> ");
	}

	private sealed record TestDomainEvent(string EventId) : IDomainEvent
	{
		public string AggregateId => "agg-1";
		public long Version => 1;
		public DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
		public string EventType => "TestDomainEvent";
		public IDictionary<string, object>? Metadata => null;
	}

	private sealed class StubPayloadSerializer(byte[] payload) : IPayloadSerializer
	{
		public int SerializeCallCount { get; private set; }

		public byte[] Serialize<T>(T value)
		{
			SerializeCallCount++;
			return payload;
		}

		public T Deserialize<T>(byte[] data) => throw new NotSupportedException();
		public byte GetCurrentSerializerId() => 1;
		public string GetCurrentSerializerName() => "stub";
		public byte[] SerializeObject(object value, Type type) => payload;
		public T DeserializeTransportMessage<T>(byte[] data) => throw new NotSupportedException();
	}

	private sealed class StubInternalSerializer(byte[] payload) : IInternalSerializer
	{
		public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter) =>
			throw new NotSupportedException();

		public byte[] Serialize<T>(T value) => payload;

		public T Deserialize<T>(ReadOnlySequence<byte> buffer) =>
			throw new NotSupportedException();

		public T Deserialize<T>(ReadOnlySpan<byte> buffer) =>
			throw new NotSupportedException();
	}
}
