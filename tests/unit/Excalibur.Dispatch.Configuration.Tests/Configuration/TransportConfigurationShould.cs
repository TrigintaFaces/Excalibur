// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="TransportConfiguration"/>.
/// </summary>
/// <remarks>
/// Tests transport configuration property defaults and behavior.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
[Trait("Priority", "0")]
public sealed class TransportConfigurationShould
{
	#region Constructor and Default Tests

	[Fact]
	public void Constructor_CreatesInstance()
	{
		// Act
		var config = new TransportConfiguration();

		// Assert
		_ = config.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_SetsNameToEmptyString()
	{
		// Act
		var config = new TransportConfiguration();

		// Assert
		config.Name.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_SetsEnabledToTrue()
	{
		// Act
		var config = new TransportConfiguration();

		// Assert
		config.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Constructor_SetsPriorityTo100()
	{
		// Act
		var config = new TransportConfiguration();

		// Assert
		config.Priority.ShouldBe(100);
	}

	#endregion

	#region Name Property Tests

	[Fact]
	public void Name_CanBeSet()
	{
		// Arrange
		var config = new TransportConfiguration();

		// Act
		config.Name = "test-transport";

		// Assert
		config.Name.ShouldBe("test-transport");
	}

	[Fact]
	public void Name_CanBeSetToNull()
	{
		// Arrange
		var config = new TransportConfiguration();

		// Act
		config.Name = null!;

		// Assert
		config.Name.ShouldBeNull();
	}

	[Theory]
	[InlineData("")]
	[InlineData("rabbitmq")]
	[InlineData("azure-servicebus")]
	[InlineData("kafka")]
	[InlineData("in-memory")]
	public void Name_AcceptsVariousValues(string name)
	{
		// Arrange
		var config = new TransportConfiguration();

		// Act
		config.Name = name;

		// Assert
		config.Name.ShouldBe(name);
	}

	#endregion

	#region Enabled Property Tests

	[Fact]
	public void Enabled_CanBeSetToFalse()
	{
		// Arrange
		var config = new TransportConfiguration();

		// Act
		config.Enabled = false;

		// Assert
		config.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Enabled_CanBeToggled()
	{
		// Arrange
		var config = new TransportConfiguration();

		// Act & Assert
		config.Enabled.ShouldBeTrue();
		config.Enabled = false;
		config.Enabled.ShouldBeFalse();
		config.Enabled = true;
		config.Enabled.ShouldBeTrue();
	}

	#endregion

	#region Priority Property Tests

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(50)]
	[InlineData(100)]
	[InlineData(1000)]
	[InlineData(int.MaxValue)]
	public void Priority_AcceptsPositiveValues(int priority)
	{
		// Arrange
		var config = new TransportConfiguration();

		// Act
		config.Priority = priority;

		// Assert
		config.Priority.ShouldBe(priority);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(-100)]
	[InlineData(int.MinValue)]
	public void Priority_AcceptsNegativeValues(int priority)
	{
		// Arrange
		var config = new TransportConfiguration();

		// Act
		config.Priority = priority;

		// Assert
		config.Priority.ShouldBe(priority);
	}

	#endregion

	#region Integration Tests

	[Fact]
	public void AllProperties_CanBeSetTogether()
	{
		// Arrange & Act
		var config = new TransportConfiguration
		{
			Name = "primary-transport",
			Enabled = false,
			Priority = 10
		};

		// Assert
		config.Name.ShouldBe("primary-transport");
		config.Enabled.ShouldBeFalse();
		config.Priority.ShouldBe(10);
	}

	[Fact]
	public void MultipleInstances_AreIndependent()
	{
		// Arrange
		var config1 = new TransportConfiguration { Name = "transport-1", Priority = 1 };
		var config2 = new TransportConfiguration { Name = "transport-2", Priority = 2 };

		// Act - modify config1
		config1.Enabled = false;

		// Assert - config2 is unaffected
		config1.Name.ShouldBe("transport-1");
		config1.Enabled.ShouldBeFalse();
		config1.Priority.ShouldBe(1);

		config2.Name.ShouldBe("transport-2");
		config2.Enabled.ShouldBeTrue();
		config2.Priority.ShouldBe(2);
	}

	#endregion
}
