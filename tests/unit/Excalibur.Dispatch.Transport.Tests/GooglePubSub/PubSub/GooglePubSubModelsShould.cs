// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.PubSub;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class GooglePubSubModelsShould
{

	[Fact]
	public void CreateSchemaDefinition()
	{
		// Arrange
		var metadata = new Dictionary<string, string> { ["owner"] = "team-a" };

		// Act
		var schema = new SchemaDefinition("schema-1", "JSON", "{\"type\":\"object\"}", metadata);

		// Assert
		schema.SchemaId.ShouldBe("schema-1");
		schema.SchemaType.ShouldBe("JSON");
		schema.Definition.ShouldBe("{\"type\":\"object\"}");
		schema.Metadata.ShouldNotBeNull();
		schema.Metadata!["owner"].ShouldBe("team-a");
	}

	[Fact]
	public void CreateSchemaDefinitionWithoutMetadata()
	{
		// Act
		var schema = new SchemaDefinition("s1", "Avro", "{}");

		// Assert
		schema.Metadata.ShouldBeNull();
	}

}
