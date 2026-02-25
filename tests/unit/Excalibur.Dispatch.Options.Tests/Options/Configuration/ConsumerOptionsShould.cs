// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.Options.Configuration;

/// <summary>
/// Unit tests for <see cref="ConsumerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class ConsumerOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Dedupe_IsNotNull()
	{
		// Arrange & Act
		var options = new ConsumerOptions();

		// Assert
		_ = options.Dedupe.ShouldNotBeNull();
	}

	[Fact]
	public void Default_Dedupe_IsDeduplicationOptionsInstance()
	{
		// Arrange & Act
		var options = new ConsumerOptions();

		// Assert
		_ = options.Dedupe.ShouldBeOfType<DeduplicationOptions>();
	}

	[Fact]
	public void Default_AckAfterHandle_IsTrue()
	{
		// Arrange & Act
		var options = new ConsumerOptions();

		// Assert
		options.AckAfterHandle.ShouldBeTrue();
	}

	[Fact]
	public void Default_MaxConcurrentMessages_IsTen()
	{
		// Arrange & Act
		var options = new ConsumerOptions();

		// Assert
		options.MaxConcurrentMessages.ShouldBe(10);
	}

	[Fact]
	public void Default_VisibilityTimeout_IsFiveMinutes()
	{
		// Arrange & Act
		var options = new ConsumerOptions();

		// Assert
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_MaxRetries_IsThree()
	{
		// Arrange & Act
		var options = new ConsumerOptions();

		// Assert
		options.MaxRetries.ShouldBe(3);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Dedupe_CanBeSet()
	{
		// Arrange
		var options = new ConsumerOptions();
		var dedupe = new DeduplicationOptions { Enabled = true };

		// Act
		options.Dedupe = dedupe;

		// Assert
		options.Dedupe.ShouldBeSameAs(dedupe);
	}

	[Fact]
	public void AckAfterHandle_CanBeSet()
	{
		// Arrange
		var options = new ConsumerOptions();

		// Act
		options.AckAfterHandle = false;

		// Assert
		options.AckAfterHandle.ShouldBeFalse();
	}

	[Fact]
	public void MaxConcurrentMessages_CanBeSet()
	{
		// Arrange
		var options = new ConsumerOptions();

		// Act
		options.MaxConcurrentMessages = 50;

		// Assert
		options.MaxConcurrentMessages.ShouldBe(50);
	}

	[Fact]
	public void VisibilityTimeout_CanBeSet()
	{
		// Arrange
		var options = new ConsumerOptions();

		// Act
		options.VisibilityTimeout = TimeSpan.FromMinutes(10);

		// Assert
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void MaxRetries_CanBeSet()
	{
		// Arrange
		var options = new ConsumerOptions();

		// Act
		options.MaxRetries = 5;

		// Assert
		options.MaxRetries.ShouldBe(5);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new ConsumerOptions
		{
			Dedupe = new DeduplicationOptions { Enabled = true },
			AckAfterHandle = false,
			MaxConcurrentMessages = 25,
			VisibilityTimeout = TimeSpan.FromMinutes(15),
			MaxRetries = 10,
		};

		// Assert
		options.Dedupe.Enabled.ShouldBeTrue();
		options.AckAfterHandle.ShouldBeFalse();
		options.MaxConcurrentMessages.ShouldBe(25);
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(15));
		options.MaxRetries.ShouldBe(10);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasHighConcurrency()
	{
		// Act
		var options = new ConsumerOptions
		{
			MaxConcurrentMessages = 100,
			AckAfterHandle = true,
		};

		// Assert
		options.MaxConcurrentMessages.ShouldBeGreaterThan(50);
		options.AckAfterHandle.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForLongRunningProcessing_HasLongerVisibilityTimeout()
	{
		// Act
		var options = new ConsumerOptions
		{
			VisibilityTimeout = TimeSpan.FromMinutes(30),
			MaxConcurrentMessages = 5,
		};

		// Assert
		options.VisibilityTimeout.ShouldBeGreaterThan(TimeSpan.FromMinutes(5));
		options.MaxConcurrentMessages.ShouldBeLessThan(10);
	}

	[Fact]
	public void Options_ForReliableProcessing_HasHighRetryCount()
	{
		// Act
		var options = new ConsumerOptions
		{
			MaxRetries = 10,
			AckAfterHandle = false,
		};

		// Assert
		options.MaxRetries.ShouldBeGreaterThan(3);
		options.AckAfterHandle.ShouldBeFalse();
	}

	#endregion
}
