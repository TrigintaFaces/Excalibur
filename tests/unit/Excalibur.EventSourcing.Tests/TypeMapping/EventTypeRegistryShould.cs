using Excalibur.EventSourcing.TypeMapping;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.TypeMapping;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventTypeRegistryShould
{
	private static EventTypeRegistry CreateRegistry(
		Dictionary<string, string>? aliases = null,
		Dictionary<string, Type>? typeMappings = null)
	{
		var options = new EventTypeRegistryOptions
		{
			Aliases = aliases ?? [],
			TypeMappings = typeMappings ?? []
		};
		return new EventTypeRegistry(Options.Create(options));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new EventTypeRegistry(null!));
	}

	[Fact]
	public void RegisterTypeAndResolveByName()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		registry.Register("OrderCreated", typeof(string));

		// Assert
		registry.ResolveType("OrderCreated").ShouldBe(typeof(string));
	}

	[Fact]
	public void ReturnNullForUnregisteredType()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		var result = registry.ResolveType("NonExistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetTypeNameForRegisteredType()
	{
		// Arrange
		var registry = CreateRegistry();
		registry.Register("OrderCreated", typeof(string));

		// Act
		var name = registry.GetTypeName(typeof(string));

		// Assert
		name.ShouldBe("OrderCreated");
	}

	[Fact]
	public void ThrowForUnregisteredTypeInGetTypeName()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => registry.GetTypeName(typeof(int)));
	}

	[Fact]
	public void PreserveFirstRegistrationAsCanonicalName()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		registry.Register("OrderCreated", typeof(string));
		registry.Register("OrderCreatedV2", typeof(string));

		// Assert - first registration wins for type-to-name mapping
		registry.GetTypeName(typeof(string)).ShouldBe("OrderCreated");
		// Both names resolve to the same type
		registry.ResolveType("OrderCreated").ShouldBe(typeof(string));
		registry.ResolveType("OrderCreatedV2").ShouldBe(typeof(string));
	}

	[Fact]
	public void ResolveAliasChain()
	{
		// Arrange
		var aliases = new Dictionary<string, string>
		{
			["OrderCreatedV1"] = "OrderCreatedV2",
			["OrderCreatedV2"] = "OrderCreated"
		};
		var typeMappings = new Dictionary<string, Type>
		{
			["OrderCreated"] = typeof(string)
		};
		var registry = CreateRegistry(aliases, typeMappings);

		// Act
		var result = registry.ResolveType("OrderCreatedV1");

		// Assert
		result.ShouldBe(typeof(string));
	}

	[Fact]
	public void HandleCircularAliasChainGracefully()
	{
		// Arrange - circular alias chain A -> B -> A
		var aliases = new Dictionary<string, string>
		{
			["TypeA"] = "TypeB",
			["TypeB"] = "TypeA"
		};
		var registry = CreateRegistry(aliases);

		// Act - should not infinite loop, max depth is 10
		var result = registry.ResolveType("TypeA");

		// Assert - returns null since no actual type mapping exists
		result.ShouldBeNull();
	}

	[Fact]
	public void RegisterTypeMappingsFromOptions()
	{
		// Arrange & Act
		var typeMappings = new Dictionary<string, Type>
		{
			["OrderCreated"] = typeof(string),
			["OrderUpdated"] = typeof(int)
		};
		var registry = CreateRegistry(typeMappings: typeMappings);

		// Assert
		registry.ResolveType("OrderCreated").ShouldBe(typeof(string));
		registry.ResolveType("OrderUpdated").ShouldBe(typeof(int));
	}

	[Fact]
	public void ThrowWhenResolveTypeCalledWithNullOrEmpty()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		Should.Throw<ArgumentException>(() => registry.ResolveType(null!));
		Should.Throw<ArgumentException>(() => registry.ResolveType(""));
	}

	[Fact]
	public void ThrowWhenGetTypeNameCalledWithNull()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => registry.GetTypeName(null!));
	}

	[Fact]
	public void ThrowWhenRegisterCalledWithNullOrEmptyName()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		Should.Throw<ArgumentException>(() => registry.Register(null!, typeof(string)));
		Should.Throw<ArgumentException>(() => registry.Register("", typeof(string)));
	}

	[Fact]
	public void ThrowWhenRegisterCalledWithNullType()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => registry.Register("Test", null!));
	}

	[Fact]
	public void ResolveDirectAlias()
	{
		// Arrange
		var aliases = new Dictionary<string, string>
		{
			["OldName"] = "NewName"
		};
		var typeMappings = new Dictionary<string, Type>
		{
			["NewName"] = typeof(string)
		};
		var registry = CreateRegistry(aliases, typeMappings);

		// Act
		var result = registry.ResolveType("OldName");

		// Assert
		result.ShouldBe(typeof(string));
	}

	[Fact]
	public void OverwriteNameToTypeMappingOnReRegister()
	{
		// Arrange
		var registry = CreateRegistry();
		registry.Register("Test", typeof(string));

		// Act - re-register same name with different type
		registry.Register("Test", typeof(int));

		// Assert - name-to-type mapping is overwritten
		registry.ResolveType("Test").ShouldBe(typeof(int));
	}
}
