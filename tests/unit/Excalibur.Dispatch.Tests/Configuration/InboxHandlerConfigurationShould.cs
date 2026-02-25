// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxHandlerConfigurationShould
{
	[Fact]
	public void Build_DefaultValues()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var settings = config.Build();

		// Assert
		settings.Retention.ShouldBe(TimeSpan.FromMinutes(1440));
		settings.UseInMemory.ShouldBeFalse();
		settings.Strategy.ShouldBe(MessageIdStrategy.FromHeader);
		settings.HeaderName.ShouldBe("MessageId");
		settings.MessageIdProviderType.ShouldBeNull();
	}

	[Fact]
	public void WithRetention_SetRetentionPeriod()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		config.WithRetention(TimeSpan.FromHours(2));
		var settings = config.Build();

		// Assert
		settings.Retention.ShouldBe(TimeSpan.FromHours(2));
	}

	[Fact]
	public void WithRetention_ThrowOnNonPositive()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(
			() => config.WithRetention(TimeSpan.Zero));
		Should.Throw<ArgumentOutOfRangeException>(
			() => config.WithRetention(TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void WithRetention_ReturnSelfForChaining()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var result = config.WithRetention(TimeSpan.FromHours(1));

		// Assert
		result.ShouldBeSameAs(config);
	}

	[Fact]
	public void UseInMemory_SetFlag()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		config.UseInMemory();
		var settings = config.Build();

		// Assert
		settings.UseInMemory.ShouldBeTrue();
	}

	[Fact]
	public void UsePersistent_ClearInMemoryFlag()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();
		config.UseInMemory();

		// Act
		config.UsePersistent();
		var settings = config.Build();

		// Assert
		settings.UseInMemory.ShouldBeFalse();
	}

	[Fact]
	public void WithStrategy_SetStrategy()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		config.WithStrategy(MessageIdStrategy.FromCorrelationId);
		var settings = config.Build();

		// Assert
		settings.Strategy.ShouldBe(MessageIdStrategy.FromCorrelationId);
	}

	[Fact]
	public void WithHeaderName_SetHeaderName()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		config.WithHeaderName("X-Custom-Id");
		var settings = config.Build();

		// Assert
		settings.HeaderName.ShouldBe("X-Custom-Id");
	}

	[Fact]
	public void WithHeaderName_ThrowOnNullOrEmpty()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act & Assert
		Should.Throw<ArgumentException>(() => config.WithHeaderName(""));
		Should.Throw<ArgumentException>(() => config.WithHeaderName("  "));
	}

	[Fact]
	public void FluentChaining_BuildWithAllOptions()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		config
			.WithRetention(TimeSpan.FromMinutes(30))
			.UseInMemory()
			.WithStrategy(MessageIdStrategy.FromHeader)
			.WithHeaderName("X-Dedup-Id");

		var settings = config.Build();

		// Assert
		settings.Retention.ShouldBe(TimeSpan.FromMinutes(30));
		settings.UseInMemory.ShouldBeTrue();
		settings.Strategy.ShouldBe(MessageIdStrategy.FromHeader);
		settings.HeaderName.ShouldBe("X-Dedup-Id");
	}
}
