// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Schema;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SchemaModelsShould
{
	[Fact]
	public void CreatePubSubSchemaInfoWithDefaults()
	{
		// Arrange & Act
		var info = new PubSubSchemaInfo();

		// Assert
		info.Name.ShouldBe(string.Empty);
		info.SchemaType.ShouldBe(string.Empty);
		info.Definition.ShouldBe(string.Empty);
		info.RevisionId.ShouldBe(string.Empty);
		info.CreatedAt.ShouldBe(default);
	}

	[Fact]
	public void AllowSettingPubSubSchemaInfoProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var info = new PubSubSchemaInfo
		{
			Name = "projects/my-project/schemas/my-schema",
			SchemaType = "PROTOCOL_BUFFER",
			Definition = "syntax = \"proto3\";",
			RevisionId = "rev-abc123",
			CreatedAt = now,
		};

		// Assert
		info.Name.ShouldBe("projects/my-project/schemas/my-schema");
		info.SchemaType.ShouldBe("PROTOCOL_BUFFER");
		info.Definition.ShouldBe("syntax = \"proto3\";");
		info.RevisionId.ShouldBe("rev-abc123");
		info.CreatedAt.ShouldBe(now);
	}

	[Fact]
	public void CreateSchemaValidationResultWithDefaults()
	{
		// Arrange & Act
		var result = new SchemaValidationResult();

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Diagnostics.ShouldNotBeNull();
		result.Diagnostics.ShouldBeEmpty();
	}

	[Fact]
	public void CreateValidSchemaValidationResult()
	{
		// Arrange & Act
		var result = new SchemaValidationResult
		{
			IsValid = true,
		};

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Diagnostics.ShouldBeEmpty();
	}

	[Fact]
	public void CreateInvalidSchemaValidationResultWithDiagnostics()
	{
		// Arrange & Act
		var result = new SchemaValidationResult
		{
			IsValid = false,
			Diagnostics = new List<string>
			{
				"Field 'name' is missing required annotation",
				"Message 'Event' has conflicting field numbers",
			},
		};

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Diagnostics.Count.ShouldBe(2);
		result.Diagnostics[0].ShouldContain("missing required annotation");
		result.Diagnostics[1].ShouldContain("conflicting field numbers");
	}
}
