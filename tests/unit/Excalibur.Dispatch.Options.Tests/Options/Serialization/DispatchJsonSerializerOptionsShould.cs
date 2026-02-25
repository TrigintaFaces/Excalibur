// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Options.Serialization;

namespace Excalibur.Dispatch.Tests.Options.Serialization;

/// <summary>
/// Unit tests for <see cref="DispatchJsonSerializerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class DispatchJsonSerializerOptionsShould
{
	#region Static Property Tests

	[Fact]
	public void Default_IsNotNull()
	{
		// Act
		var options = DispatchJsonSerializerOptions.Default;

		// Assert
		_ = options.ShouldNotBeNull();
	}

	[Fact]
	public void Default_ReturnsSameInstance()
	{
		// Act
		var options1 = DispatchJsonSerializerOptions.Default;
		var options2 = DispatchJsonSerializerOptions.Default;

		// Assert
		options1.ShouldBeSameAs(options2);
	}

	[Fact]
	public void Web_IsNotNull()
	{
		// Act
		var options = DispatchJsonSerializerOptions.Web;

		// Assert
		_ = options.ShouldNotBeNull();
	}

	[Fact]
	public void Web_ReturnsSameInstance()
	{
		// Act
		var options1 = DispatchJsonSerializerOptions.Web;
		var options2 = DispatchJsonSerializerOptions.Web;

		// Assert
		options1.ShouldBeSameAs(options2);
	}

	#endregion

	#region Web Options Configuration Tests

	[Fact]
	public void Web_HasCamelCasePropertyNaming()
	{
		// Act
		var options = DispatchJsonSerializerOptions.Web;

		// Assert
		options.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
	}

	[Fact]
	public void Web_IgnoresNullsWhenWriting()
	{
		// Act
		var options = DispatchJsonSerializerOptions.Web;

		// Assert
		options.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
	}

	[Fact]
	public void Web_WritesIndented()
	{
		// Act
		var options = DispatchJsonSerializerOptions.Web;

		// Assert
		options.WriteIndented.ShouldBeTrue();
	}

	[Fact]
	public void Web_HasJsonStringEnumConverter()
	{
		// Act
		var options = DispatchJsonSerializerOptions.Web;

		// Assert
		options.Converters.ShouldContain(c => c is JsonStringEnumConverter);
	}

	#endregion

	#region ApplyDefaults Tests

	[Fact]
	public void ApplyDefaults_ThrowsForNullOptions()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => DispatchJsonSerializerOptions.ApplyDefaults(null!));
	}

	[Fact]
	public void ApplyDefaults_SetsCamelCasePropertyNaming()
	{
		// Arrange
		var options = new JsonSerializerOptions();

		// Act
		_ = DispatchJsonSerializerOptions.ApplyDefaults(options);

		// Assert
		options.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
	}

	[Fact]
	public void ApplyDefaults_SetsIgnoreWhenWritingNull()
	{
		// Arrange
		var options = new JsonSerializerOptions();

		// Act
		_ = DispatchJsonSerializerOptions.ApplyDefaults(options);

		// Assert
		options.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
	}

	[Fact]
	public void ApplyDefaults_SetsWriteIndented()
	{
		// Arrange
		var options = new JsonSerializerOptions();

		// Act
		_ = DispatchJsonSerializerOptions.ApplyDefaults(options);

		// Assert
		options.WriteIndented.ShouldBeTrue();
	}

	[Fact]
	public void ApplyDefaults_AddsJsonStringEnumConverter()
	{
		// Arrange
		var options = new JsonSerializerOptions();

		// Act
		_ = DispatchJsonSerializerOptions.ApplyDefaults(options);

		// Assert
		options.Converters.ShouldContain(c => c is JsonStringEnumConverter);
	}

	[Fact]
	public void ApplyDefaults_ReturnsSameInstance()
	{
		// Arrange
		var options = new JsonSerializerOptions();

		// Act
		var result = DispatchJsonSerializerOptions.ApplyDefaults(options);

		// Assert
		result.ShouldBeSameAs(options);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Web_CanSerializeObject()
	{
		// Arrange
		var obj = new { Name = "Test", Value = 42 };
		var options = DispatchJsonSerializerOptions.Web;

		// Act
		var json = JsonSerializer.Serialize(obj, options);

		// Assert
		json.ShouldContain("name");
		json.ShouldContain("value");
	}

	[Fact]
	public void Web_SerializesEnumsAsStrings()
	{
		// Arrange
		var obj = new { Day = DayOfWeek.Monday };
		var options = DispatchJsonSerializerOptions.Web;

		// Act
		var json = JsonSerializer.Serialize(obj, options);

		// Assert
		json.ShouldContain("monday");
	}

	[Fact]
	public void Web_OmitsNullValues()
	{
		// Arrange
		var obj = new { Name = "Test", NullValue = (string?)null };
		var options = DispatchJsonSerializerOptions.Web;

		// Act
		var json = JsonSerializer.Serialize(obj, options);

		// Assert
		json.ShouldNotContain("nullValue");
	}

	#endregion
}
