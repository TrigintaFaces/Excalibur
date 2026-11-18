// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Excalibur.Outbox.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class OutboxMetadataWriterShould
{
	[Fact]
	public void BindAndFreezeTheActualFactoryOptionsOncePerWriter()
	{
		JsonSerializerOptions? supplied = null;
		var calls = 0;
		var configuration = new OutboxMetadataSerialization(options =>
		{
			calls++;
			supplied = options;
			return new OutboxHeaderContext(options);
		});
		var writer = configuration.CreateWriter();
		for (var index = 0; index < 3; index++) { writer.Serialize(new Dictionary<string, object> { ["number"] = index }).ShouldBe($"{{\"number\":{index}}}"); }
		calls.ShouldBe(1);
		var bound = supplied.ShouldNotBeNull();
		bound.IsReadOnly.ShouldBeTrue();
		_ = Should.Throw<InvalidOperationException>(() => bound.PropertyNamingPolicy = null);
	}

	[Fact]
	public void MatchCanonicalBytesForKeysNullsPrimitivesAndJsonElement()
	{
		using var document = JsonDocument.Parse("{\"OriginalKey\":true,\"nil\":null}");
		var headers = new Dictionary<string, object>
		{
			["VerbatimKey"] = "<value> \u263a",
			["null-entry"] = null!,
			["boolean"] = true,
			["integer"] = 123,
			["decimal"] = 12.34m,
			["element"] = document.RootElement.Clone(),
		};
		Generated().Serialize(headers).ShouldBe(JsonSerializer.Serialize(headers, EventSerializationDefaults.Canonical));
	}

	[Fact]
	public void MatchCanonicalBytesForRegisteredNestedPocoNullableEnumsAndCustomConverters()
	{
		var headers = new Dictionary<string, object>
		{
			["poco"] = new OutboxHeaderSample("name", null, OutboxHeaderState.Accepted, OutboxHeaderState.Pending,
				new OutboxHeaderChild("nested", null), new OutboxHeaderCode("a")),
			["enum"] = OutboxHeaderState.Pending,
			["undefined-enum"] = (OutboxHeaderState)99,
		};
		var json = Generated().Serialize(headers);
		json.ShouldBe(JsonSerializer.Serialize(headers, EventSerializationDefaults.Canonical));
		json.ShouldContain("\"state\":\"Accepted\"");
		json.ShouldContain("\"nullableState\":\"Pending\"");
		json.ShouldContain("\"code\":\"code:a\"");
		json.ShouldNotContain("omitted");
	}

	[Fact]
	public void RejectMissingRuntimeMetadataWithoutReflectionFallback()
	{
		var error = Should.Throw<NotSupportedException>(() => Generated().Serialize(
			new Dictionary<string, object> { ["not-registered"] = new OutboxUnregisteredHeader("hidden") }));
		error.Message.ShouldContain(typeof(OutboxUnregisteredHeader).FullName!);
	}

	[Fact]
	public void AcceptValidForwardingMetadataFromAChildContext()
	{
		var writer = OutboxMetadataWriter.Create(_ => new ForwardingResolver(OutboxHeaderContext.Default));
		writer.Serialize(new Dictionary<string, object> { ["integer"] = 7 }).ShouldBe("{\"integer\":7}");
	}

	[Fact]
	public void RejectIncorrectMetadataTypeRatherThanLosingDerivedValueData()
	{
		var writer = OutboxMetadataWriter.Create(_ => new IncorrectTypeResolver());
		_ = Should.Throw<InvalidOperationException>(() => writer.Serialize(new Dictionary<string, object> { ["integer"] = 7 }));
	}

	[Fact]
	public void RejectAContextDefaultBoundToUnrelatedOptions()
	{
		var error = Should.Throw<InvalidOperationException>(() => OutboxMetadataWriter.Create(_ => OutboxHeaderContext.Default));
		error.Message.ShouldContain("supplied canonical options");
	}

	[Fact]
	public void RejectNullFactoryOrNullResolver()
	{
		_ = Should.Throw<ArgumentNullException>(() => new OutboxMetadataSerialization(null!));
		_ = Should.Throw<ArgumentNullException>(() => OutboxMetadataWriter.Create(null!));
		_ = Should.Throw<InvalidOperationException>(() => OutboxMetadataWriter.Create(_ => null!));
	}

	[Fact]
	public void RejectOptionsFrozenWithADifferentResolver()
	{
		_ = Should.Throw<InvalidOperationException>(() => OutboxMetadataWriter.Create(options =>
		{
			options.TypeInfoResolver = new ForwardingResolver(OutboxHeaderContext.Default);
			options.MakeReadOnly();
			return new ForwardingResolver(OutboxHeaderContext.Default);
		}));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	[InlineData(6)]
	public void RejectFactoryChangesToTheFixedWireContract(int mutation)
	{
		_ = Should.Throw<InvalidOperationException>(() => OutboxMetadataWriter.Create(options =>
		{
			switch (mutation)
			{
				case 0: options.PropertyNamingPolicy = null; break;
				case 1: options.DefaultIgnoreCondition = JsonIgnoreCondition.Never; break;
				case 2: options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase; break;
				case 3: options.IncludeFields = true; break;
				case 4: options.NumberHandling = JsonNumberHandling.WriteAsString; break;
				case 5: options.Converters.Add(new JsonStringEnumConverter<OutboxHeaderState>()); break;
				case 6: options.MaxDepth = 1; break;
			}

			return new OutboxHeaderContext(options);
		}));
	}

	[Fact]
	public void PreserveEveryExistingMappedEnvelopeField()
	{
		var message = new OutboundMessage("type", [1, 2, 255], "destination", new Dictionary<string, object> { ["number"] = 7 })
		{
			Id = "id", CreatedAt = DateTimeOffset.UnixEpoch, TenantId = "tenant", LastError = "error", RetryCount = 3,
			CorrelationId = "correlation", CausationId = "causation", Priority = 8, ScheduledAt = DateTimeOffset.UnixEpoch.AddHours(2),
			PartitionKey = "partition", GroupKey = "group", SequenceNumber = 42, TargetTransports = "a,b", IsMultiTransport = true,
		};
		var generated = Generated().FromOutboundMessage(message);
		var legacy = OutboxMessage.FromOutboundMessage(message);
		generated.MessageId.ShouldBe(legacy.MessageId);
		generated.MessageType.ShouldBe(legacy.MessageType);
		generated.MessageBody.ShouldBe(legacy.MessageBody);
		generated.MessageMetadata.ShouldBe(legacy.MessageMetadata);
		generated.CreatedAt.ShouldBe(legacy.CreatedAt);
		generated.TenantId.ShouldBe(legacy.TenantId);
		generated.LastError.ShouldBe(legacy.LastError);
		generated.Attempts.ShouldBe(legacy.Attempts);
		generated.Destination.ShouldBe(legacy.Destination);
		generated.CorrelationId.ShouldBe(legacy.CorrelationId);
		generated.CausationId.ShouldBe(legacy.CausationId);
		generated.Priority.ShouldBe(legacy.Priority);
		generated.ScheduledAt.ShouldBe(legacy.ScheduledAt);
		generated.PartitionKey.ShouldBe(legacy.PartitionKey);
		generated.GroupKey.ShouldBe(legacy.GroupKey);
		generated.SequenceNumber.ShouldBe(legacy.SequenceNumber);
		generated.TargetTransports.ShouldBe(legacy.TargetTransports);
		generated.IsMultiTransport.ShouldBe(legacy.IsMultiTransport);
	}

	[Fact]
	public void KeepMetadataOnlyRegistrationFreeOfWorkersStoresAndCommonOptions()
	{
		var calls = 0;
		var configuration = new OutboxMetadataSerialization(options => { calls++; return new OutboxHeaderContext(options); });
		var services = new ServiceCollection();
		services.AddOutboxMetadataSerialization(configuration).ShouldBeSameAs(services);
		services.AddOutboxMetadataSerialization(configuration).ShouldBeSameAs(services);
		services.Count.ShouldBe(1);
		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<OutboxMetadataSerialization>().ShouldBeSameAs(configuration);
		provider.GetService<OutboxOptions>().ShouldBeNull();
		provider.GetService<IOutboxStore>().ShouldBeNull();
		provider.GetServices<IHostedService>().ShouldBeEmpty();
		calls.ShouldBe(0);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void RejectEveryConflictingDescriptorWithoutInvokingItsFactory(int kind)
	{
		var configuration = new OutboxMetadataSerialization(options => new OutboxHeaderContext(options));
		var services = new ServiceCollection();
		var invoked = false;
		switch (kind)
		{
			case 0: services.AddSingleton(new OutboxMetadataSerialization(options => new OutboxHeaderContext(options))); break;
			case 1: services.AddSingleton<OutboxMetadataSerialization>(_ => { invoked = true; return configuration; }); break;
			case 2: services.AddSingleton<OutboxMetadataSerialization>(); break;
			case 3: services.AddKeyedSingleton("key", configuration); break;
		}

		_ = Should.Throw<InvalidOperationException>(() => services.AddOutboxMetadataSerialization(configuration));
		invoked.ShouldBeFalse();
		services.Count.ShouldBe(1);
	}

	[Fact]
	public void DelegateBuilderConfigurationToTheSameMetadataOnlyRegistration()
	{
		var services = new ServiceCollection();
		var builder = new OutboxBuilder(services, new OutboxConfiguration());
		var configuration = new OutboxMetadataSerialization(options => new OutboxHeaderContext(options));
		builder.WithMetadataSerialization(configuration).ShouldBeSameAs(builder);
		services.Single().ImplementationInstance.ShouldBeSameAs(configuration);
	}

	[Fact]
	public void CreateIndependentWriterOptionsForDistinctServiceProvidersAndDirectInstances()
	{
		var captured = new List<JsonSerializerOptions>();
		var first = new OutboxMetadataSerialization(options => { captured.Add(options); return new OutboxHeaderContext(options); });
		var second = new OutboxMetadataSerialization(options => { captured.Add(options); return new OutboxHeaderContext(options); });
		using var firstProvider = new ServiceCollection().AddOutboxMetadataSerialization(first).BuildServiceProvider();
		using var secondProvider = new ServiceCollection().AddOutboxMetadataSerialization(second).BuildServiceProvider();
		var firstWriter = firstProvider.GetRequiredService<OutboxMetadataSerialization>().CreateWriter();
		var secondWriter = secondProvider.GetRequiredService<OutboxMetadataSerialization>().CreateWriter();
		captured.Count.ShouldBe(2);
		captured[0].ShouldNotBeSameAs(captured[1]);
		firstWriter.Serialize(new Dictionary<string, object> { ["integer"] = 1 }).ShouldBe("{\"integer\":1}");
		secondWriter.Serialize(new Dictionary<string, object> { ["integer"] = 2 }).ShouldBe("{\"integer\":2}");
		captured.Count.ShouldBe(2);
	}

	private static OutboxMetadataWriter Generated() => OutboxMetadataWriter.Create(options => new OutboxHeaderContext(options));

	private sealed class ForwardingResolver(JsonSerializerContext context) : IJsonTypeInfoResolver
	{
		public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) => context.GetTypeInfo(type);
	}

	private sealed class IncorrectTypeResolver : IJsonTypeInfoResolver
	{
		public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) => OutboxHeaderContext.Default.String;
	}
}

internal sealed record OutboxHeaderSample(string Name, string? Omitted, OutboxHeaderState State,
	OutboxHeaderState? NullableState, OutboxHeaderChild Child, OutboxHeaderCode Code);
internal sealed record OutboxHeaderChild(string Value, string? Omitted);
internal sealed record OutboxUnregisteredHeader(string Value);
internal enum OutboxHeaderState { Pending, Accepted }

[JsonConverter(typeof(OutboxHeaderCodeConverter))]
internal sealed record OutboxHeaderCode(string Value);

internal sealed class OutboxHeaderCodeConverter : JsonConverter<OutboxHeaderCode>
{
	public override OutboxHeaderCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		new(reader.GetString() ?? string.Empty);
	public override void Write(Utf8JsonWriter writer, OutboxHeaderCode value, JsonSerializerOptions options) =>
		writer.WriteStringValue("code:" + value.Value);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = true)]
[JsonSerializable(typeof(OutboxHeaderSample))]
[JsonSerializable(typeof(OutboxHeaderState))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class OutboxHeaderContext : JsonSerializerContext
{
}
