// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.TypeMapping;

namespace Excalibur.EventSourcing.Tests.Core.TypeMapping;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventTypeRegistryOptionsShould
{
	[Fact]
	public void InitializeAliasesAsEmptyDictionary()
	{
		// Arrange & Act
		var options = new EventTypeRegistryOptions();

		// Assert
		options.Aliases.ShouldNotBeNull();
		options.Aliases.ShouldBeEmpty();
	}

	[Fact]
	public void InitializeTypeMappingsAsEmptyDictionary()
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
		var options = new EventTypeRegistryOptions();
		var aliases = new Dictionary<string, string> { ["old"] = "new" };

		// Act
		options.Aliases = aliases;

		// Assert
		options.Aliases.ShouldBeSameAs(aliases);
	}

	[Fact]
	public void AllowSettingTypeMappings()
	{
		// Arrange
		var options = new EventTypeRegistryOptions();
		var mappings = new Dictionary<string, Type> { ["Event"] = typeof(string) };

		// Act
		options.TypeMappings = mappings;

		// Assert
		options.TypeMappings.ShouldBeSameAs(mappings);
	}
}
