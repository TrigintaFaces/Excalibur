// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class SqlServerEventStoreBehaviorShould : UnitTestBase
{
	private const string InvalidConnectionString =
		"Server=127.0.0.1,1;Database=master;User Id=sa;Password=invalid;Connect Timeout=1;Encrypt=False;TrustServerCertificate=True";

	private static readonly ILogger<SqlServerEventStore> Logger = NullLoggerFactory.CreateLogger<SqlServerEventStore>();

	[Fact]
	public async Task AppendAsync_ReturnSuccess_WhenNoEvents()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger, TestTenantContext.SingleTenantDefault);

		var result = await sut.AppendAsync("agg-1", "Order", [], expectedVersion: 5, CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(5);
		result.FirstEventPosition.ShouldBeNull();
	}

	[Fact]
	public async Task AppendAsync_ReturnFailureResult_WhenConnectionCannotBeOpened()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger, TestTenantContext.SingleTenantDefault);
		var events = new IDomainEvent[] { new TestDomainEvent("evt-1") };

		var result = await sut.AppendAsync("agg-1", "Order", events, expectedVersion: 0, CancellationToken.None);

		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeFalse();
		result.ErrorMessage!.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task LoadAsync_Throw_WhenConnectionCannotBeOpened()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger, TestTenantContext.SingleTenantDefault);

		await Should.ThrowAsync<Exception>(() =>
			sut.LoadAsync("agg-1", "Order", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task LoadAsync_FromVersion_Throw_WhenConnectionCannotBeOpened()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger, TestTenantContext.SingleTenantDefault);

		await Should.ThrowAsync<Exception>(() =>
			sut.LoadAsync("agg-1", "Order", 10, CancellationToken.None).AsTask());
	}

	[Fact]
	public void SerializeEvent_UsePayloadSerializer_WhenProvided()
	{
		var payloadSerializer = new StubPayloadSerializer([7, 8, 9]);
		var sut = new SqlServerEventStore(
			InvalidConnectionString,
			Logger,
			tenantContext: TestTenantContext.SingleTenantDefault,
			internalSerializer: null,
			payloadSerializer: payloadSerializer);
		var method = typeof(SqlServerEventStore).GetMethod("SerializeEvent", BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		// The stream identity is reported on the exception raised when a configured type-info resolver
		// does not declare the event type, so it travels with the event into serialization.
		var bytes = (byte[])method!.Invoke(sut, [new TestDomainEvent("evt-1"), "agg-1", "Order"])!;

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
			tenantContext: TestTenantContext.SingleTenantDefault,
			internalSerializer: internalSerializer,
			payloadSerializer: payloadSerializer);
		var method = typeof(SqlServerEventStore).GetMethod(
			"SerializeEventWithEnvelopeSupport",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var bytes = (byte[])method!.Invoke(sut, [Named(new TestDomainEvent("evt-2")), "agg-1", "Order", 2L])!;

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
			tenantContext: TestTenantContext.SingleTenantDefault,
			internalSerializer: null,
			payloadSerializer: payloadSerializer);
		var method = typeof(SqlServerEventStore).GetMethod(
			"SerializeEventWithEnvelopeSupport",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var bytes = (byte[])method!.Invoke(sut, [Named(new TestDomainEvent("evt-3")), "agg-1", "Order", 3L])!;

		bytes.ShouldBe([5, 6, 7]);
		payloadSerializer.SerializeCallCount.ShouldBe(1);
	}

	[Fact]
	public void SerializeMetadata_ReturnJsonPayload()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger, TestTenantContext.SingleTenantDefault);
		var method = typeof(SqlServerEventStore).GetMethod("SerializeMetadata", BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();

		var payload = (byte[])method!.Invoke(sut, [new Dictionary<string, object> { ["k"] = "v" }])!;
		payload.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public void SerializeEvent_PreserveCanonicalBytesForDerivedEventEnumsAndNulls()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger, TestTenantContext.SingleTenantDefault);
		var method = typeof(SqlServerEventStore).GetMethod("SerializeEvent", BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();
		IDomainEvent domainEvent = new DerivedWireFormatEvent("wire-event", DayOfWeek.Friday, null);
		var expected = JsonSerializer.SerializeToUtf8Bytes(domainEvent, domainEvent.GetType(), EventSerializationDefaults.CreateCanonicalOptions());
		var actual = (byte[])method.Invoke(sut, [domainEvent, "agg-1", "Order"])!;

		actual.ShouldBe(expected);
		using var json = JsonDocument.Parse(actual);
		json.RootElement.GetProperty("status").GetString().ShouldBe("Friday");
		json.RootElement.TryGetProperty("optional", out _).ShouldBeFalse();
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public void SerializeMetadata_PreserveCanonicalBytesForHeterogeneousValues()
	{
		var sut = new SqlServerEventStore(InvalidConnectionString, Logger, TestTenantContext.SingleTenantDefault);
		var method = typeof(SqlServerEventStore).GetMethod("SerializeMetadata", BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();
		IDictionary<string, object> metadata = new Dictionary<string, object>
		{
			["MixedCaseKey"] = new MetadataValue(DayOfWeek.Friday, "<quoted>\"", null),
			["explicitNull"] = null!,
			["amount"] = 12.50m,
			["numbers"] = new[] { 1, 2, 3 },
		};
		var expected = JsonSerializer.SerializeToUtf8Bytes(metadata, EventSerializationDefaults.CreateCanonicalOptions());
		var actual = (byte[])method.Invoke(sut, [metadata])!;

		actual.ShouldBe(expected);
		using var json = JsonDocument.Parse(actual);
		json.RootElement.GetProperty("MixedCaseKey").GetProperty("status").GetString().ShouldBe("Friday");
		json.RootElement.GetProperty("explicitNull").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public void SerializationFallback_RequiresResolverWhenReflectionIsDisabled()
	{
		var expectedReflection = bool.Parse(Environment.GetEnvironmentVariable("EXCALIBUR_TEST_JSON_REFLECTION") ?? "true");
		JsonSerializer.IsReflectionEnabledByDefault.ShouldBe(expectedReflection);
		if (!expectedReflection)
		{
			// Fail during construction, before a database connection or durable append is possible.
			var error = Should.Throw<InvalidOperationException>(() =>
				new SqlServerEventStore(InvalidConnectionString, Logger, TestTenantContext.SingleTenantDefault));
			error.Message.ShouldContain("EventTypeInfoResolver");
			return;
		}

		var sut = new SqlServerEventStore(InvalidConnectionString, Logger, TestTenantContext.SingleTenantDefault);
		foreach (var (name, arguments) in new (string, object?[])[]
		{
			("SerializeEvent", [new TestDomainEvent("guard-event"), "agg-1", "Order"]),
			("SerializeMetadata", [new Dictionary<string, object> { ["key"] = "value" }]),
		})
		{
			var method = typeof(SqlServerEventStore).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
			((byte[])method.Invoke(sut, arguments)!).ShouldNotBeEmpty();
		}
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public void GeneratedSerialization_PreservesEventAndMetadataWithoutReflection()
	{
		var expectedReflection = bool.Parse(Environment.GetEnvironmentVariable("EXCALIBUR_TEST_JSON_REFLECTION") ?? "true");
		JsonSerializer.IsReflectionEnabledByDefault.ShouldBe(expectedReflection);
		var sut = new SqlServerEventStore(() => new Microsoft.Data.SqlClient.SqlConnection(InvalidConnectionString),
			Logger, TestTenantContext.SingleTenantDefault, eventTypeInfoResolver: SqlWireJsonContext.Default);
		var domainEvent = new SqlWireEvent("generated-event");
		var serializeEvent = typeof(SqlServerEventStore).GetMethod("SerializeEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
		using var eventJson = JsonDocument.Parse((byte[])serializeEvent.Invoke(sut, [domainEvent, "agg-1", "Order"])!);
		eventJson.RootElement.GetProperty("eventId").GetString().ShouldBe("generated-event");
		var serializeMetadata = typeof(SqlServerEventStore).GetMethod("SerializeMetadata", BindingFlags.Instance | BindingFlags.NonPublic)!;
		using var metadataJson = JsonDocument.Parse((byte[])serializeMetadata.Invoke(sut,
			[new Dictionary<string, object> { ["MixedKey"] = "value", ["count"] = 42, ["enabled"] = true }])!);
		metadataJson.RootElement.GetProperty("MixedKey").GetString().ShouldBe("value");
		metadataJson.RootElement.GetProperty("count").GetInt32().ShouldBe(42);
		metadataJson.RootElement.GetProperty("enabled").GetBoolean().ShouldBeTrue();
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
		ex.InnerException!.ShouldBeOfType<ArgumentNullException>();

		var factory = (Func<Microsoft.Data.SqlClient.SqlConnection>)method.Invoke(null, [InvalidConnectionString])!;
		var connection = factory();
		connection.ShouldNotBeNull();
		connection.ConnectionString.ShouldContain("127.0.0.1");
	}

	[MessageName("Test.Es.SqlServerTestDomainEvent")]
	private sealed record TestDomainEvent(string EventId, IDictionary<string, object>? Metadata = null) : IDomainEvent
	{
		public string AggregateId => "agg-1";
		public long Version => 1;
		public DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
	}

	private abstract record WireFormatEvent(string EventId) : IDomainEvent
	{
		public string AggregateId => "agg-1";
		public long Version => 1;
		public DateTimeOffset OccurredAt => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		public IDictionary<string, object>? Metadata => null;
	}

	[MessageName("Test.Es.SqlServerWireFormatEvent")]
	private sealed record DerivedWireFormatEvent(string EventId, DayOfWeek Status, string? Optional) : WireFormatEvent(EventId);
	private sealed record MetadataValue(DayOfWeek Status, string Text, string? Optional);

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
		public object DeserializeObject(byte[] data, Type type) => throw new NotSupportedException();
	}

	private sealed class StubInternalSerializer(byte[] payload) : ISerializer
	{
		public string Name => "stub";
		public string Version => "1.0";
		public string ContentType => "application/octet-stream";

		public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter) =>
			bufferWriter.Write(payload);

		public T Deserialize<T>(ReadOnlySpan<byte> data) =>
			throw new NotSupportedException();

		public byte[] SerializeObject(object value, Type type) => payload;

		public object DeserializeObject(ReadOnlySpan<byte> data, Type type) =>
			throw new NotSupportedException();
	}

	private static NamedEvent Named(IDomainEvent @event) =>
		new[] { @event }.AsNamedEvents()[0];
}

[MessageName("Test.Es.SqlWireEvent")]
internal sealed record SqlWireEvent(string EventId) : IDomainEvent
{
	public string AggregateId => "agg-1";
	public long Version => 1;
	public DateTimeOffset OccurredAt => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	public IDictionary<string, object>? Metadata => null;
}

[JsonSerializable(typeof(SqlWireEvent))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
internal sealed partial class SqlWireJsonContext : JsonSerializerContext;
