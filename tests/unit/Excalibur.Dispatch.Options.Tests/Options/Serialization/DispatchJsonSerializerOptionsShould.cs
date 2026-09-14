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
[Trait(TraitNames.Component, TestComponents.Options)]
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
	public void Web_HasNoJsonStringEnumConverter()
	{
		// The converter is no longer in the shared defaults: it is built per enum type at run time,
		// so carrying it here made every consumer of these options unusable ahead-of-time whether or
		// not they wanted string enums. Web now matches JsonSerializerDefaults.Web, which writes enums
		// as numbers; ApplyDefaultsWithStringEnums is the opt-in that carries the cost.

		// Act
		var options = DispatchJsonSerializerOptions.Web;

		// Assert
		options.Converters.ShouldNotContain(c => c is JsonStringEnumConverter);
	}

	[Fact]
	public void ApplyDefaultsWithStringEnums_AddsTheConverter()
	{
		// LIVENESS for the two arms above: the opt-in must actually add what the defaults stopped
		// adding. Without this, deleting the converter everywhere would satisfy them both.

		// Arrange
		var options = new JsonSerializerOptions();

		// Act
		_ = DispatchJsonSerializerOptions.ApplyDefaultsWithStringEnums(options);

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
	public void ApplyDefaults_DoesNotAddJsonStringEnumConverter()
	{
		// Arrange
		var options = new JsonSerializerOptions();

		// Act
		_ = DispatchJsonSerializerOptions.ApplyDefaults(options);

		// Assert
		options.Converters.ShouldNotContain(c => c is JsonStringEnumConverter);
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
	public void Web_SerializesEnumsAsNumbers()
	{
		// This is the WIRE-FORMAT half of the change, and it is the one a consumer feels: enums used
		// to be written as camelCased names and are now written as numbers, matching
		// JsonSerializerDefaults.Web. Asserting the number rather than merely the absence of the name
		// keeps this arm RED if the converter ever returns to the shared defaults.

		// Arrange
		var obj = new { Day = DayOfWeek.Monday };
		var options = DispatchJsonSerializerOptions.Web;

		// Act
		var json = JsonSerializer.Serialize(obj, options);

		// Assert
		json.ShouldContain("1");
		json.ShouldNotContain("monday");
	}

	[Fact]
	public void ApplyDefaultsWithStringEnums_SerializesEnumsAsCamelCasedNames()
	{
		// LIVENESS partner: the opt-in still produces the old wire format for anyone who needs it,
		// so the change is a relocation of the behaviour rather than its removal.

		// Arrange
		var obj = new { Day = DayOfWeek.Monday };
		var options = DispatchJsonSerializerOptions.ApplyDefaultsWithStringEnums(new JsonSerializerOptions());

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
