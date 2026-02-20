// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="MessageEnvelopePoolOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MessageEnvelopePoolOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_ThreadLocalCacheSize_Is16()
	{
		// Arrange & Act
		var options = new MessageEnvelopePoolOptions();

		// Assert
		options.ThreadLocalCacheSize.ShouldBe(16);
	}

	[Fact]
	public void Default_EnableTelemetry_IsFalse()
	{
		// Arrange & Act
		var options = new MessageEnvelopePoolOptions();

		// Assert
		options.EnableTelemetry.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void ThreadLocalCacheSize_CanBeSet()
	{
		// Arrange
		var options = new MessageEnvelopePoolOptions();

		// Act
		options.ThreadLocalCacheSize = 32;

		// Assert
		options.ThreadLocalCacheSize.ShouldBe(32);
	}

	[Fact]
	public void EnableTelemetry_CanBeSet()
	{
		// Arrange
		var options = new MessageEnvelopePoolOptions();

		// Act
		options.EnableTelemetry = true;

		// Assert
		options.EnableTelemetry.ShouldBeTrue();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new MessageEnvelopePoolOptions
		{
			ThreadLocalCacheSize = 64,
			EnableTelemetry = true,
		};

		// Assert
		options.ThreadLocalCacheSize.ShouldBe(64);
		options.EnableTelemetry.ShouldBeTrue();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForThroughput_HasLargeCache()
	{
		// Act
		var options = new MessageEnvelopePoolOptions
		{
			ThreadLocalCacheSize = 64,
			EnableTelemetry = false,
		};

		// Assert
		options.ThreadLocalCacheSize.ShouldBeGreaterThan(16);
		options.EnableTelemetry.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForObservability_EnablesTelemetry()
	{
		// Act
		var options = new MessageEnvelopePoolOptions
		{
			EnableTelemetry = true,
		};

		// Assert
		options.EnableTelemetry.ShouldBeTrue();
	}

	#endregion
}
