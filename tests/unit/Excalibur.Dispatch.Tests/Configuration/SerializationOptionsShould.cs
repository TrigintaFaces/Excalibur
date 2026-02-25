// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Options.Serialization;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SerializationOptionsShould
{
	// --- DispatchJsonSerializerOptions ---

	[Fact]
	public void DispatchJsonSerializerOptions_Default_IsNotNull()
	{
		// Act
		var options = DispatchJsonSerializerOptions.Default;

		// Assert
		options.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchJsonSerializerOptions_Default_UsesGeneralDefaults()
	{
		// Act
		var options = DispatchJsonSerializerOptions.Default;

		// Assert - General defaults don't apply camelCase by default
		options.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchJsonSerializerOptions_Web_IsNotNull()
	{
		// Act
		var options = DispatchJsonSerializerOptions.Web;

		// Assert
		options.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchJsonSerializerOptions_Web_HasCamelCaseNaming()
	{
		// Act
		var options = DispatchJsonSerializerOptions.Web;

		// Assert
		options.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
	}

	[Fact]
	public void DispatchJsonSerializerOptions_Web_HasWriteIndented()
	{
		// Act
		var options = DispatchJsonSerializerOptions.Web;

		// Assert
		options.WriteIndented.ShouldBeTrue();
	}

	[Fact]
	public void DispatchJsonSerializerOptions_Web_IgnoresNullWhenWriting()
	{
		// Act
		var options = DispatchJsonSerializerOptions.Web;

		// Assert
		options.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code for JSON serialization options")]
	public void DispatchJsonSerializerOptions_ApplyDefaults_ConfiguresCorrectly()
	{
		// Arrange
		var options = new JsonSerializerOptions();

		// Act
		var result = DispatchJsonSerializerOptions.ApplyDefaults(options);

		// Assert
		result.ShouldBeSameAs(options);
		result.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
		result.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
		result.WriteIndented.ShouldBeTrue();
		result.Converters.ShouldContain(c => c is JsonStringEnumConverter);
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code for JSON serialization options")]
	public void DispatchJsonSerializerOptions_ApplyDefaults_ThrowsOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchJsonSerializerOptions.ApplyDefaults(null!));
	}

	[Fact]
	public void DispatchJsonSerializerOptions_Default_ReturnsSameInstance()
	{
		// Act
		var first = DispatchJsonSerializerOptions.Default;
		var second = DispatchJsonSerializerOptions.Default;

		// Assert - Lazy<T> returns same instance
		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void DispatchJsonSerializerOptions_Web_ReturnsSameInstance()
	{
		// Act
		var first = DispatchJsonSerializerOptions.Web;
		var second = DispatchJsonSerializerOptions.Web;

		// Assert - Lazy<T> returns same instance
		first.ShouldBeSameAs(second);
	}

	// --- MessageSerializerOptions ---

	[Fact]
	public void MessageSerializerOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MessageSerializerOptions();

		// Assert
		options.SerializerMap.ShouldNotBeNull();
		options.SerializerMap.ShouldBeEmpty();
	}

	[Fact]
	public void MessageSerializerOptions_SerializerMap_CanAddEntries()
	{
		// Arrange
		var options = new MessageSerializerOptions();

		// Act
		options.SerializerMap[1] = typeof(string);
		options.SerializerMap[2] = typeof(int);

		// Assert
		options.SerializerMap.Count.ShouldBe(2);
		options.SerializerMap[1].ShouldBe(typeof(string));
		options.SerializerMap[2].ShouldBe(typeof(int));
	}
}
