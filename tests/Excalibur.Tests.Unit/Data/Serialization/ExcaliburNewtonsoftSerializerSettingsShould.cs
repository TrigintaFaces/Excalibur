using Excalibur.Data.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Shouldly;

namespace Excalibur.Tests.Unit.Data.Serialization;

public class ExcaliburNewtonsoftSerializerSettingsShould
{
	[Fact]
	public void ConfigureDefaultSettingsWithCamelCase()
	{
		// Act
		var settings = ExcaliburNewtonsoftSerializerSettings.Default;

		// Assert
		_ = settings.ContractResolver.ShouldBeOfType<CamelCasePropertyNamesContractResolver>();
		settings.NullValueHandling.ShouldBe(NullValueHandling.Ignore);
		settings.Formatting.ShouldBe(Formatting.Indented);
	}

	[Fact]
	public void SerializePropertiesWithCamelCase()
	{
		// Arrange
		var testObject = new TestClass { PropertyOne = "value1", PropertyTwo = 123 };

		// Act
		var json = JsonConvert.SerializeObject(testObject, ExcaliburNewtonsoftSerializerSettings.Default);

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
		var result = JsonConvert.DeserializeObject<TestClass>(json, ExcaliburNewtonsoftSerializerSettings.Default);

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
		var json = JsonConvert.SerializeObject(testObject, ExcaliburNewtonsoftSerializerSettings.Default);

		// Assert
		json.ShouldNotContain("propertyOne");
		var deserialized = JsonConvert.DeserializeObject<TestClass>(json, ExcaliburNewtonsoftSerializerSettings.Default);
		deserialized.PropertyTwo.ShouldBe(123);
	}

	private sealed class TestClass
	{
		public string? PropertyOne { get; set; }
		public int PropertyTwo { get; set; }
	}
}
