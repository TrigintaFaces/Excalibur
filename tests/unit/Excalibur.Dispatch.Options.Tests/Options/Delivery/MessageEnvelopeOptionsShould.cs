// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="MessageEnvelopeOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MessageEnvelopeOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_ThreadLocalCacheSize_Is16()
	{
		// Arrange & Act
		var options = new MessageEnvelopeOptions();

		// Assert
		options.ThreadLocalCacheSize.ShouldBe(16);
	}

	[Fact]
	public void Default_EnableTelemetry_IsFalse()
	{
		// Arrange & Act
		var options = new MessageEnvelopeOptions();

		// Assert
		options.EnableTelemetry.ShouldBeFalse();
	}

	[Fact]
	public void Default_PoolContexts_IsTrue()
	{
		// Arrange & Act
		var options = new MessageEnvelopeOptions();

		// Assert
		options.PoolContexts.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void ThreadLocalCacheSize_CanBeSet()
	{
		// Arrange
		var options = new MessageEnvelopeOptions();

		// Act
		options.ThreadLocalCacheSize = 32;

		// Assert
		options.ThreadLocalCacheSize.ShouldBe(32);
	}

	[Fact]
	public void EnableTelemetry_CanBeSet()
	{
		// Arrange
		var options = new MessageEnvelopeOptions();

		// Act
		options.EnableTelemetry = true;

		// Assert
		options.EnableTelemetry.ShouldBeTrue();
	}

	[Fact]
	public void PoolContexts_CanBeSet()
	{
		// Arrange
		var options = new MessageEnvelopeOptions();

		// Act
		options.PoolContexts = false;

		// Assert
		options.PoolContexts.ShouldBeFalse();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new MessageEnvelopeOptions
		{
			ThreadLocalCacheSize = 64,
			EnableTelemetry = true,
			PoolContexts = false,
		};

		// Assert
		options.ThreadLocalCacheSize.ShouldBe(64);
		options.EnableTelemetry.ShouldBeTrue();
		options.PoolContexts.ShouldBeFalse();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForThroughput_HasLargeCacheAndPooling()
	{
		// Act
		var options = new MessageEnvelopeOptions
		{
			ThreadLocalCacheSize = 64,
			PoolContexts = true,
			EnableTelemetry = false,
		};

		// Assert
		options.ThreadLocalCacheSize.ShouldBeGreaterThan(16);
		options.PoolContexts.ShouldBeTrue();
		options.EnableTelemetry.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForObservability_EnablesTelemetry()
	{
		// Act
		var options = new MessageEnvelopeOptions
		{
			EnableTelemetry = true,
		};

		// Assert
		options.EnableTelemetry.ShouldBeTrue();
	}

	#endregion
}
