// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="JsonSerializationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class JsonSerializationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_JsonSerializerOptions_IsNotNull()
	{
		// Arrange & Act
		var options = new JsonSerializationOptions();

		// Assert
		_ = options.JsonSerializerOptions.ShouldNotBeNull();
	}

	[Fact]
	public void Default_PreserveReferences_IsFalse()
	{
		// Arrange & Act
		var options = new JsonSerializationOptions();

		// Assert
		options.PreserveReferences.ShouldBeFalse();
	}

	[Fact]
	public void Default_MaxDepth_IsSixtyFour()
	{
		// Arrange & Act
		var options = new JsonSerializationOptions();

		// Assert
		options.MaxDepth.ShouldBe(64);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void JsonSerializerOptions_CanBeSet()
	{
		// Arrange
		var options = new JsonSerializationOptions();
		var serializerOptions = new JsonSerializerOptions
		{
			WriteIndented = true,
		};

		// Act
		options.JsonSerializerOptions = serializerOptions;

		// Assert
		options.JsonSerializerOptions.ShouldBeSameAs(serializerOptions);
		options.JsonSerializerOptions.WriteIndented.ShouldBeTrue();
	}

	[Fact]
	public void PreserveReferences_CanBeSet()
	{
		// Arrange
		var options = new JsonSerializationOptions();

		// Act
		options.PreserveReferences = true;

		// Assert
		options.PreserveReferences.ShouldBeTrue();
	}

	[Fact]
	public void MaxDepth_CanBeSet()
	{
		// Arrange
		var options = new JsonSerializationOptions();

		// Act
		options.MaxDepth = 128;

		// Assert
		options.MaxDepth.ShouldBe(128);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var serializerOptions = new JsonSerializerOptions { WriteIndented = true };

		// Act
		var options = new JsonSerializationOptions
		{
			JsonSerializerOptions = serializerOptions,
			PreserveReferences = true,
			MaxDepth = 32,
		};

		// Assert
		options.JsonSerializerOptions.WriteIndented.ShouldBeTrue();
		options.PreserveReferences.ShouldBeTrue();
		options.MaxDepth.ShouldBe(32);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForDeepNestedObjects_HasHighMaxDepth()
	{
		// Act
		var options = new JsonSerializationOptions
		{
			MaxDepth = 256,
		};

		// Assert
		options.MaxDepth.ShouldBeGreaterThan(64);
	}

	[Fact]
	public void Options_ForCircularReferences_EnablesPreserveReferences()
	{
		// Act
		var options = new JsonSerializationOptions
		{
			PreserveReferences = true,
		};

		// Assert
		options.PreserveReferences.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForReadableOutput_HasIndentedOutput()
	{
		// Act
		var options = new JsonSerializationOptions
		{
			JsonSerializerOptions = new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			},
		};

		// Assert
		options.JsonSerializerOptions.WriteIndented.ShouldBeTrue();
		options.JsonSerializerOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
	}

	[Fact]
	public void Options_ForSecurityRestrictedDepth_HasLowMaxDepth()
	{
		// Act
		var options = new JsonSerializationOptions
		{
			MaxDepth = 16,
		};

		// Assert
		options.MaxDepth.ShouldBeLessThan(64);
	}

	#endregion
}
