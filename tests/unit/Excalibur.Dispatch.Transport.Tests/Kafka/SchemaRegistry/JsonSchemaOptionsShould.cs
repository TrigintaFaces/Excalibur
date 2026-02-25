// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for the <see cref="JsonSchemaOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify default values and property setters.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class JsonSchemaOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void IncludeAnnotations_DefaultsToFalse()
	{
		// Act
		var options = new JsonSchemaOptions();

		// Assert
		options.IncludeAnnotations.ShouldBeFalse();
	}

	[Fact]
	public void AllowAdditionalProperties_DefaultsToTrue()
	{
		// Act
		var options = new JsonSchemaOptions();

		// Assert
		options.AllowAdditionalProperties.ShouldBeTrue();
	}

	[Fact]
	public void JsonSerializerOptions_DefaultsToNull()
	{
		// Act
		var options = new JsonSchemaOptions();

		// Assert
		options.JsonSerializerOptions.ShouldBeNull();
	}

	[Fact]
	public void TreatNullObliviousAsNonNullable_DefaultsToTrue()
	{
		// Act
		var options = new JsonSchemaOptions();

		// Assert
		options.TreatNullObliviousAsNonNullable.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void IncludeAnnotations_CanBeSetToTrue()
	{
		// Act
		var options = new JsonSchemaOptions { IncludeAnnotations = true };

		// Assert
		options.IncludeAnnotations.ShouldBeTrue();
	}

	[Fact]
	public void AllowAdditionalProperties_CanBeSetToFalse()
	{
		// Act
		var options = new JsonSchemaOptions { AllowAdditionalProperties = false };

		// Assert
		options.AllowAdditionalProperties.ShouldBeFalse();
	}

	[Fact]
	public void JsonSerializerOptions_CanBeSet()
	{
		// Arrange
		var serializerOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = true
		};

		// Act
		var options = new JsonSchemaOptions { JsonSerializerOptions = serializerOptions };

		// Assert
		options.JsonSerializerOptions.ShouldBe(serializerOptions);
		options.JsonSerializerOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
		options.JsonSerializerOptions.WriteIndented.ShouldBeTrue();
	}

	[Fact]
	public void TreatNullObliviousAsNonNullable_CanBeSetToFalse()
	{
		// Act
		var options = new JsonSchemaOptions { TreatNullObliviousAsNonNullable = false };

		// Assert
		options.TreatNullObliviousAsNonNullable.ShouldBeFalse();
	}

	#endregion

	#region Initialization Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var serializerOptions = new JsonSerializerOptions();

		// Act
		var options = new JsonSchemaOptions
		{
			IncludeAnnotations = true,
			AllowAdditionalProperties = false,
			JsonSerializerOptions = serializerOptions,
			TreatNullObliviousAsNonNullable = false
		};

		// Assert
		options.IncludeAnnotations.ShouldBeTrue();
		options.AllowAdditionalProperties.ShouldBeFalse();
		options.JsonSerializerOptions.ShouldBe(serializerOptions);
		options.TreatNullObliviousAsNonNullable.ShouldBeFalse();
	}

	#endregion

	#region Class Tests

	[Fact]
	public void Class_IsSealed()
	{
		// Assert
		typeof(JsonSchemaOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void AllProperties_AreWritable()
	{
		// Assert
		typeof(JsonSchemaOptions).GetProperty(nameof(JsonSchemaOptions.IncludeAnnotations)).CanWrite.ShouldBeTrue();
		typeof(JsonSchemaOptions).GetProperty(nameof(JsonSchemaOptions.AllowAdditionalProperties)).CanWrite.ShouldBeTrue();
		typeof(JsonSchemaOptions).GetProperty(nameof(JsonSchemaOptions.JsonSerializerOptions)).CanWrite.ShouldBeTrue();
		typeof(JsonSchemaOptions).GetProperty(nameof(JsonSchemaOptions.TreatNullObliviousAsNonNullable)).CanWrite.ShouldBeTrue();
	}

	#endregion
}
