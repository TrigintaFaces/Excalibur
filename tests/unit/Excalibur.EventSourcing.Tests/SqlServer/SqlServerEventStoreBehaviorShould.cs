// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Tests.SqlServer;

[Trait("Category", "Unit")]
public sealed class SqlServerEventStoreBehaviorShould : UnitTestBase
{
	private const string InvalidConnectionString =
		"Server=127.0.0.1,1;Database=master;User Id=sa;Password=invalid;Connect Timeout=1;Encrypt=False;TrustServerCertificate=True";

	private static readonly ILogger<SqlServerEventStore> Logger = NullLoggerFactory.CreateLogger<SqlServerEventStore>();

	[Fact]
	public async Task AppendAsync_ReturnSuccess_WhenNoEvents()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger);

		var result = await sut.AppendAsync("agg-1", "Order", [], expectedVersion: 5, CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(5);
		result.FirstEventPosition.ShouldBe(0);
	}

	[Fact]
	public async Task AppendAsync_ReturnFailureResult_WhenConnectionCannotBeOpened()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger);
		var events = new IDomainEvent[] { new TestDomainEvent("evt-1") };

		var result = await sut.AppendAsync("agg-1", "Order", events, expectedVersion: 0, CancellationToken.None);

		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task LoadAsync_Throw_WhenConnectionCannotBeOpened()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger);

		await Should.ThrowAsync<Exception>(() =>
			sut.LoadAsync("agg-1", "Order", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task LoadAsync_FromVersion_Throw_WhenConnectionCannotBeOpened()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger);

		await Should.ThrowAsync<Exception>(() =>
			sut.LoadAsync("agg-1", "Order", 10, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_Throw_WhenConnectionCannotBeOpened()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger);

		await Should.ThrowAsync<Exception>(() =>
			sut.GetUndispatchedEventsAsync(100, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_Throw_WhenConnectionCannotBeOpened()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger);

		await Should.ThrowAsync<Exception>(() =>
			sut.MarkEventAsDispatchedAsync("evt-1", CancellationToken.None).AsTask());
	}

	[Fact]
	public void SerializeEvent_UsePayloadSerializer_WhenProvided()
	{
		var payloadSerializer = new StubPayloadSerializer([7, 8, 9]);
		var sut = new SqlServerEventStore(
			InvalidConnectionString,
			Logger,
			internalSerializer: null,
			payloadSerializer: payloadSerializer);
		var method = typeof(SqlServerEventStore).GetMethod("SerializeEvent", BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var bytes = (byte[])method!.Invoke(sut, [new TestDomainEvent("evt-1")])!;

		bytes.ShouldBe([7, 8, 9]);
		payloadSerializer.SerializeCallCount.ShouldBe(1);
	}

	[Fact]
	public void SerializeEventWithEnvelopeSupport_PrependFormatMarker_WhenInternalSerializerConfigured()
	{
		var payloadSerializer = new StubPayloadSerializer([1, 2, 3, 4]);
		var internalSerializer = new StubInternalSerializer([9, 10, 11]);
		var sut = new SqlServerEventStore(
			InvalidConnectionString,
			Logger,
			internalSerializer: internalSerializer,
			payloadSerializer: payloadSerializer);
		var method = typeof(SqlServerEventStore).GetMethod(
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
		var sut = new SqlServerEventStore(
			InvalidConnectionString,
			Logger,
			internalSerializer: null,
			payloadSerializer: payloadSerializer);
		var method = typeof(SqlServerEventStore).GetMethod(
			"SerializeEventWithEnvelopeSupport",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var bytes = (byte[])method!.Invoke(sut, [new TestDomainEvent("evt-3"), "agg-1", "Order", 3L])!;

		bytes.ShouldBe([5, 6, 7]);
		payloadSerializer.SerializeCallCount.ShouldBe(1);
	}

	[Fact]
	public void SerializeMetadata_ReturnJsonPayload()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger);
		var method = typeof(SqlServerEventStore).GetMethod("SerializeMetadata", BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var payload = (byte[])method!.Invoke(sut, [new Dictionary<string, object> { ["k"] = "v" }])!;
		payload.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void GetFullExceptionMessage_FlattenInnerExceptionChain()
	{
		var method = typeof(SqlServerEventStore).GetMethod("GetFullExceptionMessage", BindingFlags.Static | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var ex = new InvalidOperationException("outer", new Exception("middle", new Exception("inner")));
		var message = (string)method!.Invoke(null, [ex])!;

		message.ShouldContain("outer");
		message.ShouldContain("middle");
		message.ShouldContain("inner");
		message.ShouldContain(" -> ");
	}

	[Fact]
	public void GetFullExceptionMessage_ReturnSingleMessage_WhenNoInnerException()
	{
		var method = typeof(SqlServerEventStore).GetMethod("GetFullExceptionMessage", BindingFlags.Static | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var message = (string)method!.Invoke(null, [new InvalidOperationException("only")])!;
		message.ShouldBe("only");
	}

	[Fact]
	public void ExtractCorrelationId_ResolveBothKeyCasings()
	{
		var method = typeof(SqlServerEventStore).GetMethod("ExtractCorrelationId", BindingFlags.Static | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var upper = new IDomainEvent[] { new TestDomainEvent("evt-1", new Dictionary<string, object> { ["CorrelationId"] = "c1" }) };
		method!.Invoke(null, [upper]).ShouldBe("c1");

		var lower = new IDomainEvent[] { new TestDomainEvent("evt-2", new Dictionary<string, object> { ["correlationId"] = "c2" }) };
		method.Invoke(null, [lower]).ShouldBe("c2");

		var none = new IDomainEvent[] { new TestDomainEvent("evt-3", null) };
		method.Invoke(null, [none]).ShouldBeNull();
	}

	[Fact]
	public void ExtractEventId_ReturnFirstNonEmptyId()
	{
		var method = typeof(SqlServerEventStore).GetMethod("ExtractEventId", BindingFlags.Static | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var events = new IDomainEvent[] { new TestDomainEvent("", null), new TestDomainEvent("evt-9", null) };
		method!.Invoke(null, [events]).ShouldBe("evt-9");
	}

	[Fact]
	public void CreateConnectionFactory_ValidateNullAndCreateConnection()
	{
		var method = typeof(SqlServerEventStore).GetMethod("CreateConnectionFactory", BindingFlags.Static | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var ex = Should.Throw<TargetInvocationException>(() => method!.Invoke(null, [null]));
		ex.InnerException.ShouldBeOfType<ArgumentNullException>();

		var factory = (Func<Microsoft.Data.SqlClient.SqlConnection>)method.Invoke(null, [InvalidConnectionString])!;
		var connection = factory();
		connection.ShouldNotBeNull();
		connection.ConnectionString.ShouldContain("127.0.0.1");
	}

	private sealed record TestDomainEvent(string EventId, IDictionary<string, object>? Metadata = null) : IDomainEvent
	{
		public string AggregateId => "agg-1";
		public long Version => 1;
		public DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
		public string EventType => "TestDomainEvent";
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
