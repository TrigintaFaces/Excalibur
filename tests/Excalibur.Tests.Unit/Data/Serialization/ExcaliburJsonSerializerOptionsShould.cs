using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Data.Serialization;

using Shouldly;

namespace Excalibur.Tests.Unit.Data.Serialization;

public class ExcaliburJsonSerializerOptionsShould
{
	[Fact]
	public void ConfigureDefaultOptionsWithCamelCase()
	{
		// Act
		var options = ExcaliburJsonSerializerOptions.Web;

		// Assert
		options.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
		options.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
		options.WriteIndented.ShouldBeTrue();
		options.PropertyNameCaseInsensitive.ShouldBeTrue();
	}

	[Fact]
	public void SerializePropertiesWithCamelCase()
	{
		// Arrange
		var testObject = new TestClass { PropertyOne = "value1", PropertyTwo = 123 };

		// Act
		var json = JsonSerializer.Serialize(testObject, ExcaliburJsonSerializerOptions.Web);

		// Assert
		json.ShouldContain("\"propertyOne\"");
		json.ShouldContain("\"propertyTwo\"");
	}

	[Fact]
	public void DeserializePropertiesWithCamelCase()
	{
		// Arrange
		var json = "{\"propertyOne\":\"value1\",\"propertyTwo\":123}";

		// Act
		var result = JsonSerializer.Deserialize<TestClass>(json, ExcaliburJsonSerializerOptions.Web);

		// Assert
		_ = result.ShouldNotBeNull();
		result.PropertyOne.ShouldBe("value1");
		result.PropertyTwo.ShouldBe(123);
	}

	[Fact]
	public void IgnoreNullValuesWhenSerializing()
	{
		// Arrange
		var testObject = new TestClass { PropertyOne = null, PropertyTwo = 123 };

		// Act
		var json = JsonSerializer.Serialize(testObject, ExcaliburJsonSerializerOptions.Web);

		// Assert
		json.ShouldNotContain("propertyOne");
		json.ShouldContain("\"propertyTwo\"");
	}

	private sealed class TestClass
	{
		public string? PropertyOne { get; set; }
		public int PropertyTwo { get; set; }
	}
}
