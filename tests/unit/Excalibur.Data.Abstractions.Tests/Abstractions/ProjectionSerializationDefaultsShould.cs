// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// The read-model wire contract belongs to the store that wrote the document, never to the generated
/// metadata a caller happens to hold. A projection read through a context that declares a different
/// naming policy, or omits a converter the write path applied, returns defaults or wrong values with
/// no exception, which is the failure no consumer can detect from outside.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
public sealed class ProjectionSerializationDefaultsShould
{
	// Divides by 100 on read and multiplies on write, so a dropped converter is visible as a wrong
	// NUMBER rather than a missing one -- the write-path-fidelity half of the contract.
	private sealed class ScaledDecimalConverter : JsonConverter<decimal>
	{
		public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal() / 100m;
		public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteNumberValue(value * 100m);
	}

	private static JsonSerializerOptions WriteOptions()
	{
		var options = ProjectionSerializationDefaults.CreateReadModelOptions();
		options.Converters.Add(new ScaledDecimalConverter());
		return options;
	}

	private static string Persist(SampleProjection projection) =>
		JsonSerializer.Serialize(projection, (JsonTypeInfo<SampleProjection>)
			new JsonSerializerOptions(WriteOptions()) { TypeInfoResolver = PascalCaseContext.Default }.GetTypeInfo(typeof(SampleProjection)));

	// "pascal-case" is the realistic default: a consumer context declaring no naming policy at all.
	// "snake-case" disagrees in the OTHER direction, so a fix that merely hard-codes camelCase fails it.
	private static JsonSerializerContext Context(string label) => label switch
	{
		"pascal-case" => PascalCaseContext.Default,
		"snake-case" => SnakeCaseContext.Default,
		_ => throw new ArgumentOutOfRangeException(nameof(label)),
	};

	[Theory]
	[InlineData("pascal-case")]
	[InlineData("snake-case")]
	public void ReadBackEveryValueWhenTheGeneratedContextDisagreesWithTheWritePath(string label)
	{
		var written = Persist(new SampleProjection { CustomerName = "Ada", OrderCount = 7, Total = 12.5m, Pinned = "p" });
		var supplied = (JsonTypeInfo<SampleProjection>)Context(label).GetTypeInfo(typeof(SampleProjection))!;

		var effective = ProjectionSerializationDefaults.CreateReadModelTypeInfo(WriteOptions(), supplied);
		var read = JsonSerializer.Deserialize(written, effective);

		read.ShouldNotBeNull(label);
		read.CustomerName.ShouldBe("Ada", $"[{label}] a conventionally named property must survive the round trip");
		read.OrderCount.ShouldBe(7, $"[{label}] a conventionally named property must survive the round trip");
		read.Total.ShouldBe(12.5m, $"[{label}] the write path's converter must be applied on read");
		read.Pinned.ShouldBe("p", $"[{label}] an explicitly named property is immune to the naming policy");
	}

	/// <summary>
	/// The liveness half. Without it, a derivation that returned metadata reading nothing at all would
	/// still satisfy every assertion above about the DEFECT being absent.
	/// </summary>
	[Fact]
	public void LeaveAnAlreadyAgreeingContextReadingExactlyWhatItReadBefore()
	{
		var written = Persist(new SampleProjection { CustomerName = "Grace", OrderCount = 3, Total = 4.25m, Pinned = "q" });
		var agreeing = (JsonTypeInfo<SampleProjection>)
			new JsonSerializerOptions(WriteOptions()) { TypeInfoResolver = PascalCaseContext.Default }.GetTypeInfo(typeof(SampleProjection));

		var effective = ProjectionSerializationDefaults.CreateReadModelTypeInfo(WriteOptions(), agreeing);
		var read = JsonSerializer.Deserialize(written, effective);

		read.ShouldNotBeNull();
		read.CustomerName.ShouldBe("Grace");
		read.OrderCount.ShouldBe(3);
		read.Total.ShouldBe(4.25m);
		read.Pinned.ShouldBe("q");
	}

	/// <summary>Proves the arms above would actually catch the defect, so a green cannot be read as vacuous.</summary>
	[Fact]
	public void DemonstrateThatReadingThroughTheSuppliedMetadataDirectlyIsTheSilentDefect()
	{
		var written = Persist(new SampleProjection { CustomerName = "Ada", OrderCount = 7, Total = 12.5m, Pinned = "p" });
		var supplied = (JsonTypeInfo<SampleProjection>)PascalCaseContext.Default.GetTypeInfo(typeof(SampleProjection))!;

		var naive = JsonSerializer.Deserialize(written, supplied);

		naive.ShouldNotBeNull("the defect is silent: deserialization SUCCEEDS");
		naive.CustomerName.ShouldBeNull("every conventionally named property reads as its default");
		naive.OrderCount.ShouldBe(0);
		naive.Pinned.ShouldBe("p");
	}

	[Fact]
	public void RefuseMetadataCarryingNoResolverRatherThanReadingTheWrongShape()
	{
		var orphan = JsonTypeInfo.CreateJsonTypeInfo<SampleProjection>(new JsonSerializerOptions());

		_ = Should.Throw<NotSupportedException>(() => ProjectionSerializationDefaults.CreateReadModelTypeInfo(WriteOptions(), orphan));
	}
}

public sealed class SampleProjection
{
	public string? CustomerName { get; set; }
	public int OrderCount { get; set; }
	public decimal Total { get; set; }
	[JsonPropertyName("explicitly_named")] public string? Pinned { get; set; }
}

[JsonSerializable(typeof(SampleProjection))]
public sealed partial class PascalCaseContext : JsonSerializerContext;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(SampleProjection))]
public sealed partial class SnakeCaseContext : JsonSerializerContext;
