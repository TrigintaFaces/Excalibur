// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Options;

namespace Excalibur.Dispatch.Abstractions.Tests.Options;

/// <summary>
/// Unit tests for <see cref="TransportBindingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class TransportBindingOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void Default_PriorityIsZero()
	{
		// Arrange & Act
		var options = new TransportBindingOptions();

		// Assert
		options.Priority.ShouldBe(0);
	}

	[Fact]
	public void Default_EnabledIsTrue()
	{
		// Arrange & Act
		var options = new TransportBindingOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_PropertiesIsEmptyDictionary()
	{
		// Arrange & Act
		var options = new TransportBindingOptions();

		// Assert
		_ = options.Properties.ShouldNotBeNull();
		options.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void Default_MaxConcurrencyIsProcessorCount()
	{
		// Arrange & Act
		var options = new TransportBindingOptions();

		// Assert
		options.MaxConcurrency.ShouldBe(Environment.ProcessorCount);
	}

	[Fact]
	public void Default_ProcessingTimeoutIs5Minutes()
	{
		// Arrange & Act
		var options = new TransportBindingOptions();

		// Assert
		options.ProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_UseDeadLetterQueueIsTrue()
	{
		// Arrange & Act
		var options = new TransportBindingOptions();

		// Assert
		options.UseDeadLetterQueue.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Priority_CanBeSet()
	{
		// Arrange
		var options = new TransportBindingOptions();

		// Act
		options.Priority = 100;

		// Assert
		options.Priority.ShouldBe(100);
	}

	[Fact]
	public void Priority_CanBeNegative()
	{
		// Arrange
		var options = new TransportBindingOptions();

		// Act
		options.Priority = -50;

		// Assert
		options.Priority.ShouldBe(-50);
	}

	[Fact]
	public void Enabled_CanBeSetToFalse()
	{
		// Arrange
		var options = new TransportBindingOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void MaxConcurrency_CanBeSet()
	{
		// Arrange
		var options = new TransportBindingOptions();

		// Act
		options.MaxConcurrency = 32;

		// Assert
		options.MaxConcurrency.ShouldBe(32);
	}

	[Fact]
	public void ProcessingTimeout_CanBeSet()
	{
		// Arrange
		var options = new TransportBindingOptions();

		// Act
		options.ProcessingTimeout = TimeSpan.FromMinutes(10);

		// Assert
		options.ProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void UseDeadLetterQueue_CanBeSetToFalse()
	{
		// Arrange
		var options = new TransportBindingOptions();

		// Act
		options.UseDeadLetterQueue = false;

		// Assert
		options.UseDeadLetterQueue.ShouldBeFalse();
	}

	#endregion

	#region Properties Dictionary Tests

	[Fact]
	public void Properties_CanAddEntry()
	{
		// Arrange
		var options = new TransportBindingOptions();

		// Act
		options.Properties["key"] = "value";

		// Assert
		options.Properties.ShouldContainKeyAndValue("key", "value");
	}

	[Fact]
	public void Properties_CanAddMultipleEntries()
	{
		// Arrange
		var options = new TransportBindingOptions();

		// Act
		options.Properties["key1"] = "value1";
		options.Properties["key2"] = 42;
		options.Properties["key3"] = true;

		// Assert
		options.Properties.Count.ShouldBe(3);
	}

	[Fact]
	public void Properties_UsesCaseSensitiveComparison()
	{
		// Arrange
		var options = new TransportBindingOptions();

		// Act
		options.Properties["Key"] = "value1";
		options.Properties["key"] = "value2";

		// Assert - Case-sensitive, so these should be different keys
		options.Properties.Count.ShouldBe(2);
	}

	[Fact]
	public void Properties_CanBeInitialized()
	{
		// Act
		var options = new TransportBindingOptions
		{
			Properties = new Dictionary<string, object>
			{
				["preset"] = "value",
			},
		};

		// Assert
		options.Properties.ShouldContainKeyAndValue("preset", "value");
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new TransportBindingOptions
		{
			Priority = 50,
			Enabled = false,
			MaxConcurrency = 16,
			ProcessingTimeout = TimeSpan.FromMinutes(2),
			UseDeadLetterQueue = false,
		};

		// Assert
		options.Priority.ShouldBe(50);
		options.Enabled.ShouldBeFalse();
		options.MaxConcurrency.ShouldBe(16);
		options.ProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.UseDeadLetterQueue.ShouldBeFalse();
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void MaxConcurrency_CanBeZero()
	{
		// Arrange
		var options = new TransportBindingOptions();

		// Act
		options.MaxConcurrency = 0;

		// Assert
		options.MaxConcurrency.ShouldBe(0);
	}

	[Fact]
	public void ProcessingTimeout_CanBeZero()
	{
		// Arrange
		var options = new TransportBindingOptions();

		// Act
		options.ProcessingTimeout = TimeSpan.Zero;

		// Assert
		options.ProcessingTimeout.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ProcessingTimeout_CanBeInfinite()
	{
		// Arrange
		var options = new TransportBindingOptions();

		// Act
		options.ProcessingTimeout = Timeout.InfiniteTimeSpan;

		// Assert
		options.ProcessingTimeout.ShouldBe(Timeout.InfiniteTimeSpan);
	}

	#endregion
}
