// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Nodes;

using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
public sealed class ProjectionMappingCompatibilityShould
{
	private const string Declared = """
		{"status":{"type":"keyword"},"excaliburProjectionPosition":{"type":"long"},
		 "address":{"type":"object","properties":{"code":{"type":"keyword"}}}}
		""";

	[Fact]
	public void AcceptRequiredFieldsWithAdditionalUnrelatedFields()
	{
		var actual = JsonNode.Parse(Declared)!.AsObject();
		actual["extra"] = JsonNode.Parse("""{"type":"text"}""");
		ProjectionMappingCompatibility.FindMismatch(JsonNode.Parse(Declared)!.AsObject(), actual).ShouldBeNull();
	}

	[Theory]
	[InlineData("""{"type":"text","fields":{"keyword":{"type":"keyword","ignore_above":256}}}""")]
	[InlineData("""{"type":"keyword","normalizer":"lowercase"}""")]
	[InlineData("""{"type":"keyword","index":false}""")]
	[InlineData("""{"type":"keyword","doc_values":false}""")]
	[InlineData("""{"type":"keyword","ignore_above":256}""")]
	public void RefuseIncompatibleExactMatchSemantics(string storedStatus)
	{
		var actual = JsonNode.Parse(Declared)!.AsObject();
		actual["status"] = JsonNode.Parse(storedStatus);
		ProjectionMappingCompatibility.FindMismatch(JsonNode.Parse(Declared)!.AsObject(), actual)
			.ShouldNotBeNull();
	}

	[Fact]
	public void RefuseMissingReservedPosition()
	{
		var actual = JsonNode.Parse(Declared)!.AsObject();
		actual.Remove("excaliburProjectionPosition");
		ProjectionMappingCompatibility.FindMismatch(JsonNode.Parse(Declared)!.AsObject(), actual)
			.ShouldContain("excaliburProjectionPosition");
	}

	[Fact]
	public void RefuseIncorrectReservedPositionType()
	{
		var actual = JsonNode.Parse(Declared)!.AsObject();
		actual["excaliburProjectionPosition"] = JsonNode.Parse("""{"type":"keyword"}""");
		ProjectionMappingCompatibility.FindMismatch(JsonNode.Parse(Declared)!.AsObject(), actual)
			.ShouldContain("excaliburProjectionPosition.type");
	}

	[Fact]
	public void CheckNestedRequiredFields()
	{
		var actual = JsonNode.Parse(Declared)!.AsObject();
		actual["address"]!["properties"]!["code"] = JsonNode.Parse("""{"type":"text"}""");
		ProjectionMappingCompatibility.FindMismatch(JsonNode.Parse(Declared)!.AsObject(), actual)
			.ShouldContain("address.properties.code.type");
	}

	[Fact]
	public void AcceptExplicitDeclaredNormalizerAndKeywordSubfield()
	{
		const string fields = """
			{"status":{"type":"text","fields":{"keyword":{"type":"keyword","normalizer":"lowercase"}}}}
			""";
		ProjectionMappingCompatibility.FindMismatch(JsonNode.Parse(fields)!.AsObject(), JsonNode.Parse(fields)!.AsObject())
			.ShouldBeNull();
	}

	[Fact]
	public void KeepSearchVisibilityOptIn()
	{
		new ElasticSearchProjectionIndexOptions().WaitForSearchVisibilityOnWrite.ShouldBeFalse();
	}

	[Fact]
	public void RejectWaitingForADisabledRefreshEvenWithASuppliedClient()
	{
		var options = new ElasticSearchProjectionStoreOptions
		{
			ClientSuppliedByRegistration = true,
			Index = { WaitForSearchVisibilityOnWrite = true, RefreshInterval = "-1" },
		};
		Should.Throw<InvalidOperationException>(options.Validate);
	}
	[Theory]
	[InlineData("date", "strict_date_optional_time||epoch_millis")]
	[InlineData("date_nanos", "strict_date_optional_time_nanos||epoch_millis")]
	public void AcceptFreshMappingWithOmittedDefaultFormatButRejectCustomServerFormat(string type, string format)
	{
		var expected = new JsonObject { ["created"] = new JsonObject { ["type"] = type, ["format"] = format } };
		var actual = new JsonObject { ["created"] = new JsonObject { ["type"] = type } };
		ProjectionMappingCompatibility.FindMismatch(expected, actual).ShouldBeNull();
		actual["created"]!["format"] = "yyyy-MM-dd";
		ProjectionMappingCompatibility.FindMismatch(expected, actual).ShouldContain("created.format");
	}

	[Theory]
	[InlineData("date", "yyyy-MM-dd")]
	[InlineData("date_nanos", "strict_date_optional_time||epoch_millis")]
	public void RejectOmittedFormatForANonDefaultDeclaration(string type, string format)
	{
		var expected = new JsonObject { ["created"] = new JsonObject { ["type"] = type, ["format"] = format } };
		var actual = new JsonObject { ["created"] = new JsonObject { ["type"] = type } };
		ProjectionMappingCompatibility.FindMismatch(expected, actual).ShouldContain("created.format");
	}

	[Theory]
	[InlineData("index")]
	[InlineData("doc_values")]
	public void AcceptOmittedTrueDefaultButRejectFalseOrNonDefaultDeclaration(string parameter)
	{
		var expected = new JsonObject { ["status"] = new JsonObject { ["type"] = "keyword", [parameter] = true } };
		var actual = new JsonObject { ["status"] = new JsonObject { ["type"] = "keyword" } };
		ProjectionMappingCompatibility.FindMismatch(expected, actual).ShouldBeNull();
		actual["status"]![parameter] = false;
		ProjectionMappingCompatibility.FindMismatch(expected, actual).ShouldContain(parameter);
		expected["status"]![parameter] = false;
		actual["status"]!.AsObject().Remove(parameter);
		ProjectionMappingCompatibility.FindMismatch(expected, actual).ShouldContain(parameter);
	}
}
