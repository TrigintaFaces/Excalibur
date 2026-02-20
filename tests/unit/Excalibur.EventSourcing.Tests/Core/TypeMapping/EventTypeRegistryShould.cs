// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.TypeMapping;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.TypeMapping;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventTypeRegistryShould
{
	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new EventTypeRegistry(null!));
	}

	[Fact]
	public void RegisterExplicitTypeMapping()
	{
		// Arrange
		var options = Options.Create(new EventTypeRegistryOptions
		{
			TypeMappings = new Dictionary<string, Type>
			{
				["OrderCreated"] = typeof(string)
			}
		});

		// Act
		var registry = new EventTypeRegistry(options);

		// Assert
		registry.ResolveType("OrderCreated").ShouldBe(typeof(string));
	}

	[Fact]
	public void ReturnNullForUnregisteredType()
	{
		// Arrange
		var options = Options.Create(new EventTypeRegistryOptions());
		var registry = new EventTypeRegistry(options);

		// Act
		var result = registry.ResolveType("NonExistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetTypeNameForRegisteredType()
	{
		// Arrange
		var options = Options.Create(new EventTypeRegistryOptions
		{
			TypeMappings = new Dictionary<string, Type>
			{
				["OrderCreated"] = typeof(string)
			}
		});
		var registry = new EventTypeRegistry(options);

		// Act
		var name = registry.GetTypeName(typeof(string));

		// Assert
		name.ShouldBe("OrderCreated");
	}

	[Fact]
	public void ThrowWhenGetTypeNameForUnregisteredType()
	{
		// Arrange
		var options = Options.Create(new EventTypeRegistryOptions());
		var registry = new EventTypeRegistry(options);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			registry.GetTypeName(typeof(int)));
	}

	[Fact]
	public void ResolveAlias()
	{
		// Arrange
		var options = Options.Create(new EventTypeRegistryOptions
		{
			Aliases = new Dictionary<string, string>
			{
				["OldName"] = "NewName"
			},
			TypeMappings = new Dictionary<string, Type>
			{
				["NewName"] = typeof(string)
			}
		});
		var registry = new EventTypeRegistry(options);

		// Act
		var result = registry.ResolveType("OldName");

		// Assert
		result.ShouldBe(typeof(string));
	}

	[Fact]
	public void ResolveChainedAliases()
	{
		// Arrange
		var options = Options.Create(new EventTypeRegistryOptions
		{
			Aliases = new Dictionary<string, string>
			{
				["V1Name"] = "V2Name",
				["V2Name"] = "V3Name"
			},
			TypeMappings = new Dictionary<string, Type>
			{
				["V3Name"] = typeof(string)
			}
		});
		var registry = new EventTypeRegistry(options);

		// Act
		var result = registry.ResolveType("V1Name");

		// Assert
		result.ShouldBe(typeof(string));
	}

	[Fact]
	public void RegisterManuallyViaRegisterMethod()
	{
		// Arrange
		var options = Options.Create(new EventTypeRegistryOptions());
		var registry = new EventTypeRegistry(options);

		// Act
		registry.Register("CustomEvent", typeof(int));

		// Assert
		registry.ResolveType("CustomEvent").ShouldBe(typeof(int));
		registry.GetTypeName(typeof(int)).ShouldBe("CustomEvent");
	}

	[Fact]
	public void ThrowWhenResolveTypeWithNullOrEmptyName()
	{
		// Arrange
		var options = Options.Create(new EventTypeRegistryOptions());
		var registry = new EventTypeRegistry(options);

		// Act & Assert
		Should.Throw<ArgumentException>(() => registry.ResolveType(null!));
		Should.Throw<ArgumentException>(() => registry.ResolveType(""));
	}

	[Fact]
	public void ThrowWhenGetTypeNameWithNullType()
	{
		// Arrange
		var options = Options.Create(new EventTypeRegistryOptions());
		var registry = new EventTypeRegistry(options);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => registry.GetTypeName(null!));
	}

	[Fact]
	public void ThrowWhenRegisterWithNullOrEmptyName()
	{
		// Arrange
		var options = Options.Create(new EventTypeRegistryOptions());
		var registry = new EventTypeRegistry(options);

		// Act & Assert
		Should.Throw<ArgumentException>(() => registry.Register(null!, typeof(string)));
		Should.Throw<ArgumentException>(() => registry.Register("", typeof(string)));
	}

	[Fact]
	public void ThrowWhenRegisterWithNullType()
	{
		// Arrange
		var options = Options.Create(new EventTypeRegistryOptions());
		var registry = new EventTypeRegistry(options);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => registry.Register("Name", null!));
	}

	[Fact]
	public void PreserveFirstTypeToNameMapping()
	{
		// Arrange — register same type under two names, first wins
		var options = Options.Create(new EventTypeRegistryOptions
		{
			TypeMappings = new Dictionary<string, Type>
			{
				["FirstName"] = typeof(string),
				["SecondName"] = typeof(string)
			}
		});
		var registry = new EventTypeRegistry(options);

		// Act
		var name = registry.GetTypeName(typeof(string));

		// Assert — first registration wins as canonical
		name.ShouldBe("FirstName");
	}

	[Fact]
	public void ImplementIEventTypeRegistry()
	{
		// Arrange
		var options = Options.Create(new EventTypeRegistryOptions());

		// Act
		var registry = new EventTypeRegistry(options);

		// Assert
		registry.ShouldBeAssignableTo<IEventTypeRegistry>();
	}
}
