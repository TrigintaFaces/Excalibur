using Excalibur.EventSourcing.TypeMapping;

namespace Excalibur.EventSourcing.Tests.TypeMapping;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventTypeRegistryOptionsShould
{
	[Fact]
	public void HaveEmptyAliasesByDefault()
	{
		// Arrange & Act
		var options = new EventTypeRegistryOptions();

		// Assert
		options.Aliases.ShouldNotBeNull();
		options.Aliases.ShouldBeEmpty();
	}

	[Fact]
	public void HaveEmptyTypeMappingsByDefault()
	{
		// Arrange & Act
		var options = new EventTypeRegistryOptions();

		// Assert
		options.TypeMappings.ShouldNotBeNull();
		options.TypeMappings.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAliases()
	{
		// Arrange
		var aliases = new Dictionary<string, string>
		{
			["OldType"] = "NewType"
		};

		// Act
		var options = new EventTypeRegistryOptions { Aliases = aliases };

		// Assert
		options.Aliases.ShouldContainKey("OldType");
		options.Aliases["OldType"].ShouldBe("NewType");
	}

	[Fact]
	public void AllowSettingTypeMappings()
	{
		// Arrange
		var mappings = new Dictionary<string, Type>
		{
			["OrderCreated"] = typeof(string)
		};

		// Act
		var options = new EventTypeRegistryOptions { TypeMappings = mappings };

		// Assert
		options.TypeMappings.ShouldContainKey("OrderCreated");
		options.TypeMappings["OrderCreated"].ShouldBe(typeof(string));
	}
}
