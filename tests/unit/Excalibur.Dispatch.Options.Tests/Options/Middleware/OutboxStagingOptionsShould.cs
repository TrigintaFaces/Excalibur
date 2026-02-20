// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="OutboxStagingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class OutboxStagingOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new OutboxStagingOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_MaxOutboundMessagesPerOperation_IsOneHundred()
	{
		// Arrange & Act
		var options = new OutboxStagingOptions();

		// Assert
		options.MaxOutboundMessagesPerOperation.ShouldBe(100);
	}

	[Fact]
	public void Default_CompressMessageData_IsFalse()
	{
		// Arrange & Act
		var options = new OutboxStagingOptions();

		// Assert
		options.CompressMessageData.ShouldBeFalse();
	}

	[Fact]
	public void Default_BypassOutboxForTypes_IsNull()
	{
		// Arrange & Act
		var options = new OutboxStagingOptions();

		// Assert
		options.BypassOutboxForTypes.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new OutboxStagingOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void MaxOutboundMessagesPerOperation_CanBeSet()
	{
		// Arrange
		var options = new OutboxStagingOptions();

		// Act
		options.MaxOutboundMessagesPerOperation = 500;

		// Assert
		options.MaxOutboundMessagesPerOperation.ShouldBe(500);
	}

	[Fact]
	public void CompressMessageData_CanBeSet()
	{
		// Arrange
		var options = new OutboxStagingOptions();

		// Act
		options.CompressMessageData = true;

		// Assert
		options.CompressMessageData.ShouldBeTrue();
	}

	[Fact]
	public void BypassOutboxForTypes_CanBeSet()
	{
		// Arrange
		var options = new OutboxStagingOptions();

		// Act
		options.BypassOutboxForTypes = ["InternalEvent", "LogMessage"];

		// Assert
		_ = options.BypassOutboxForTypes.ShouldNotBeNull();
		options.BypassOutboxForTypes.Length.ShouldBe(2);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new OutboxStagingOptions
		{
			Enabled = false,
			MaxOutboundMessagesPerOperation = 250,
			CompressMessageData = true,
			BypassOutboxForTypes = ["Test"],
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.MaxOutboundMessagesPerOperation.ShouldBe(250);
		options.CompressMessageData.ShouldBeTrue();
		_ = options.BypassOutboxForTypes.ShouldNotBeNull();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighVolume_HasLargeMessageLimit()
	{
		// Act
		var options = new OutboxStagingOptions
		{
			MaxOutboundMessagesPerOperation = 1000,
			Enabled = true,
		};

		// Assert
		options.MaxOutboundMessagesPerOperation.ShouldBeGreaterThan(100);
	}

	[Fact]
	public void Options_ForBandwidthConstrained_HasCompression()
	{
		// Act
		var options = new OutboxStagingOptions
		{
			CompressMessageData = true,
			Enabled = true,
		};

		// Assert
		options.CompressMessageData.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForSelectiveOutbox_HasBypassTypes()
	{
		// Act
		var options = new OutboxStagingOptions
		{
			BypassOutboxForTypes = ["HealthCheck", "Ping", "Metrics"],
		};

		// Assert
		_ = options.BypassOutboxForTypes.ShouldNotBeNull();
		options.BypassOutboxForTypes.Length.ShouldBeGreaterThan(1);
	}

	#endregion
}
