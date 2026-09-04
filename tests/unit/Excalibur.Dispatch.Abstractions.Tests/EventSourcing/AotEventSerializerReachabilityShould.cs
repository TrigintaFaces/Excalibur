// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// Binds the reachability and wire-compatibility contract of the source-generated event serializer: it
/// resolves as <see cref="IEventSerializer"/> through a real container, and the bytes it writes are
/// identical to those written by the reflection-based serializer that owns the stored format.
/// </summary>
/// <remarks>
/// Parity is proven by comparing emitted bytes and by reading each serializer's output with the other,
/// over a populated event carrying a null property, an enum, and metadata — the three axes on which a
/// divergent serializer configuration silently mis-reads rather than failing.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Feature", "AOT")]
public sealed class AotEventSerializerReachabilityShould
{
	private static ReachabilityOrderPlaced PopulatedEvent() => new()
	{
		EventId = "evt-7",
		OrderId = "ORD-7",
		Status = ReachabilityOrderStatus.Shipped,
		CancellationReason = null,
		OccurredAt = new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero),
		Metadata = new Dictionary<string, object>
		{
			["CorrelationId"] = "corr-7",
			["Attempt"] = 3,
			["Replayed"] = true,
		},
	};

	private static IEventSerializer ReflectionSerializer() => new JsonEventSerializer();

	private static AotJsonEventSerializer AotSerializer() =>
		new(ReachabilityJsonContext.Default, typeof(ReachabilityOrderPlaced));

	[Fact]
	public void ResolveThroughTheContainerAsTheEventSerializer()
	{
		var services = new ServiceCollection();
		services.AddEventTypes<ReachabilityOrderPlaced>();
		services.AddAotEventSerializer(ReachabilityJsonContext.Default);

		using var provider = services.BuildServiceProvider();

		_ = provider.GetRequiredService<IEventSerializer>().ShouldBeOfType<AotJsonEventSerializer>();
	}

	[Fact]
	public void ResolveThroughTheContainerWhateverOrderRegistrationRuns()
	{
		// AddDispatch registers the reflection serializer with TryAdd, so a plain Add here would leave the
		// winner dependent on call order. Mimic the later-registration case directly.
		var services = new ServiceCollection();
		services.AddSingleton<IEventSerializer>(new JsonEventSerializer());
		services.AddEventTypes<ReachabilityOrderPlaced>();
		services.AddAotEventSerializer(ReachabilityJsonContext.Default);

		using var provider = services.BuildServiceProvider();

		_ = provider.GetRequiredService<IEventSerializer>().ShouldBeOfType<AotJsonEventSerializer>();
		provider.GetServices<IEventSerializer>().Count().ShouldBe(1);
	}

	[Fact]
	public void RoundTripThroughTheContainerResolvedSerializer()
	{
		var services = new ServiceCollection();
		services.AddEventTypes<ReachabilityOrderPlaced>();
		services.AddAotEventSerializer(ReachabilityJsonContext.Default);

		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetRequiredService<IEventSerializer>();

		var typeName = serializer.GetTypeName(typeof(ReachabilityOrderPlaced));
		var bytes = serializer.SerializeEvent(PopulatedEvent());

		serializer.ResolveType(typeName).ShouldBe(typeof(ReachabilityOrderPlaced));

		var result = serializer
			.DeserializeEvent(bytes, serializer.ResolveType(typeName))
			.ShouldBeOfType<ReachabilityOrderPlaced>();

		result.OrderId.ShouldBe("ORD-7");
		result.Status.ShouldBe(ReachabilityOrderStatus.Shipped);
		result.CancellationReason.ShouldBeNull();
		result.Metadata!.Count.ShouldBe(3);
	}

	[Fact]
	public void WriteBytesIdenticalToTheReflectionSerializer()
	{
		var @event = PopulatedEvent();

		var aotBytes = AotSerializer().SerializeEvent(@event);
		var reflectionBytes = ReflectionSerializer().SerializeEvent(@event);

		// Compared as text so a failure names the diverging key instead of a byte offset.
		Encoding.UTF8.GetString(aotBytes).ShouldBe(Encoding.UTF8.GetString(reflectionBytes));
	}

	[Fact]
	public void ReadEventsWrittenByTheReflectionSerializer()
	{
		var written = ReflectionSerializer().SerializeEvent(PopulatedEvent());

		var result = AotSerializer()
			.DeserializeEvent(written, typeof(ReachabilityOrderPlaced))
			.ShouldBeOfType<ReachabilityOrderPlaced>();

		result.OrderId.ShouldBe("ORD-7");
		result.Status.ShouldBe(ReachabilityOrderStatus.Shipped);
		result.CancellationReason.ShouldBeNull();
		result.Metadata!.Count.ShouldBe(3);
	}

	[Fact]
	public void WriteEventsTheReflectionSerializerCanRead()
	{
		var written = AotSerializer().SerializeEvent(PopulatedEvent());

		var result = ReflectionSerializer()
			.DeserializeEvent(written, typeof(ReachabilityOrderPlaced))
			.ShouldBeOfType<ReachabilityOrderPlaced>();

		result.OrderId.ShouldBe("ORD-7");
		result.Status.ShouldBe(ReachabilityOrderStatus.Shipped);
		result.CancellationReason.ShouldBeNull();
		result.Metadata!.Count.ShouldBe(3);
	}

	[Fact]
	public void EmitCamelCaseKeysStringEnumsAndOmittedNulls()
	{
		var json = Encoding.UTF8.GetString(AotSerializer().SerializeEvent(PopulatedEvent()));

		json.ShouldContain("\"orderId\":\"ORD-7\"");
		json.ShouldContain("\"status\":\"Shipped\"");
		json.ShouldNotContain("cancellationReason");
	}

	[Fact]
	public void RejectAContextThatWouldWriteAnIncompatibleWireShape()
	{
		// Non-vacuity for the parity guard: a context declared without [JsonSourceGenerationOptions] is
		// exactly what a consumer gets by forgetting the attribute, and it must not be accepted silently.
		var ex = Should.Throw<ArgumentException>(() =>
			new AotJsonEventSerializer(DivergentJsonContext.Default, typeof(ReachabilityOrderPlaced)));

		ex.Message.ShouldContain("camelCase");
		ex.Message.ShouldContain("JsonSourceGenerationOptions");
	}

	[Fact]
	public void RejectACorrectlyAnnotatedContextSuppliedAsANewInstance()
	{
		// The generated [JsonSourceGenerationOptions] settings are applied to the Default singleton only; a
		// freshly constructed context carries default PascalCase, null-writing options despite the attribute.
		// Passing one is the likelier mistake than omitting the attribute, so it must be refused too.
		var ex = Should.Throw<ArgumentException>(() =>
			new AotJsonEventSerializer(new ReachabilityJsonContext(), typeof(ReachabilityOrderPlaced)));

		ex.Message.ShouldContain("Default");
	}

	[Fact]
	public void RejectTheDivergentContextThroughTheContainerToo()
	{
		var services = new ServiceCollection();
		services.AddEventTypes<ReachabilityOrderPlaced>();
		services.AddAotEventSerializer(DivergentJsonContext.Default);

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<ArgumentException>(() => provider.GetRequiredService<IEventSerializer>());
	}

	internal enum ReachabilityOrderStatus
	{
		Placed = 0,
		Shipped = 1,
	}

	[MessageName("Test.Aot.ReachabilityOrderPlaced")]
	internal sealed class ReachabilityOrderPlaced : IDomainEvent
	{
		public string OrderId { get; set; } = string.Empty;

		public ReachabilityOrderStatus Status { get; set; }

		public string? CancellationReason { get; set; }

		public string EventId { get; set; } = string.Empty;

		public DateTimeOffset OccurredAt { get; set; }


		public IDictionary<string, object>? Metadata { get; set; }
	}
}

/// <summary>
/// The canonical event context shape consumers are told to declare: camelCase, enums as strings, nulls
/// omitted, plus the closed metadata value types the application actually stores.
/// </summary>
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	UseStringEnumConverter = true,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AotEventSerializerReachabilityShould.ReachabilityOrderPlaced))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
internal sealed partial class ReachabilityJsonContext : JsonSerializerContext;

/// <summary>
/// A context declared without the canonical settings — the shape a consumer produces by forgetting the
/// attribute, and the one the serializer must refuse.
/// </summary>
[JsonSerializable(typeof(AotEventSerializerReachabilityShould.ReachabilityOrderPlaced))]
internal sealed partial class DivergentJsonContext : JsonSerializerContext;
