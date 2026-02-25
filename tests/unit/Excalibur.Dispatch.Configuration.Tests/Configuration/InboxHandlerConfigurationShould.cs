// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Unit tests for the <see cref="InboxHandlerConfiguration"/> class.
/// </summary>
/// <remarks>
/// Sprint 446 S446.5: Unit tests for handler-level inbox configuration.
/// Tests fluent configuration methods and built settings.
/// Note: InboxHandlerConfiguration is internal, accessed via InternalsVisibleTo.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxHandlerConfigurationShould
{
	#region WithRetention Tests

	[Fact]
	public void ReturnSameInstance_WhenWithRetentionIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var result = config.WithRetention(TimeSpan.FromHours(12));

		// Assert
		result.ShouldBeSameAs(config);
	}

	[Fact]
	public void ApplyRetention_WhenWithRetentionIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();
		var expectedRetention = TimeSpan.FromHours(48);

		// Act
		_ = config.WithRetention(expectedRetention);
		var settings = config.Build();

		// Assert
		settings.Retention.ShouldBe(expectedRetention);
	}

	[Fact]
	public void ThrowArgumentOutOfRangeException_WhenRetentionIsZero()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			config.WithRetention(TimeSpan.Zero));
	}

	[Fact]
	public void ThrowArgumentOutOfRangeException_WhenRetentionIsNegative()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			config.WithRetention(TimeSpan.FromHours(-1)));
	}

	[Fact]
	public void UseDefaultRetentionOf24Hours_WhenNotConfigured()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var settings = config.Build();

		// Assert
		settings.Retention.ShouldBe(TimeSpan.FromHours(24));
	}

	#endregion

	#region UseInMemory Tests

	[Fact]
	public void ReturnSameInstance_WhenUseInMemoryIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var result = config.UseInMemory();

		// Assert
		result.ShouldBeSameAs(config);
	}

	[Fact]
	public void SetUseInMemoryToTrue_WhenUseInMemoryIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		_ = config.UseInMemory();
		var settings = config.Build();

		// Assert
		settings.UseInMemory.ShouldBeTrue();
	}

	[Fact]
	public void DefaultToNotUseInMemory_WhenNotConfigured()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var settings = config.Build();

		// Assert
		settings.UseInMemory.ShouldBeFalse();
	}

	#endregion

	#region UsePersistent Tests

	[Fact]
	public void ReturnSameInstance_WhenUsePersistentIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var result = config.UsePersistent();

		// Assert
		result.ShouldBeSameAs(config);
	}

	[Fact]
	public void SetUseInMemoryToFalse_WhenUsePersistentIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();
		_ = config.UseInMemory(); // Set to in-memory first

		// Act
		_ = config.UsePersistent();
		var settings = config.Build();

		// Assert
		settings.UseInMemory.ShouldBeFalse();
	}

	#endregion

	#region WithStrategy Tests

	[Fact]
	public void ReturnSameInstance_WhenWithStrategyIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var result = config.WithStrategy(MessageIdStrategy.FromCorrelationId);

		// Assert
		result.ShouldBeSameAs(config);
	}

	[Fact]
	public void ApplyStrategy_WhenWithStrategyIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		_ = config.WithStrategy(MessageIdStrategy.FromCorrelationId);
		var settings = config.Build();

		// Assert
		settings.Strategy.ShouldBe(MessageIdStrategy.FromCorrelationId);
	}

	[Fact]
	public void DefaultToFromHeaderStrategy_WhenNotConfigured()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var settings = config.Build();

		// Assert
		settings.Strategy.ShouldBe(MessageIdStrategy.FromHeader);
	}

	#endregion

	#region WithHeaderName Tests

	[Fact]
	public void ReturnSameInstance_WhenWithHeaderNameIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var result = config.WithHeaderName("CustomMessageId");

		// Assert
		result.ShouldBeSameAs(config);
	}

	[Fact]
	public void ApplyHeaderName_WhenWithHeaderNameIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();
		const string customHeader = "X-Custom-Id";

		// Act
		_ = config.WithHeaderName(customHeader);
		var settings = config.Build();

		// Assert
		settings.HeaderName.ShouldBe(customHeader);
	}

	[Fact]
	public void ThrowArgumentException_WhenHeaderNameIsNull()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			config.WithHeaderName(null!));
	}

	[Fact]
	public void ThrowArgumentException_WhenHeaderNameIsEmpty()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			config.WithHeaderName(string.Empty));
	}

	[Fact]
	public void ThrowArgumentException_WhenHeaderNameIsWhitespace()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			config.WithHeaderName("   "));
	}

	[Fact]
	public void DefaultToMessageIdHeader_WhenNotConfigured()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var settings = config.Build();

		// Assert
		settings.HeaderName.ShouldBe("MessageId");
	}

	#endregion

	#region WithMessageIdProvider Tests

	[Fact]
	public void ReturnSameInstance_WhenWithMessageIdProviderIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var result = config.WithMessageIdProvider<TestMessageIdProvider>();

		// Assert
		result.ShouldBeSameAs(config);
	}

	[Fact]
	public void SetProviderType_WhenWithMessageIdProviderIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		_ = config.WithMessageIdProvider<TestMessageIdProvider>();
		var settings = config.Build();

		// Assert
		settings.MessageIdProviderType.ShouldBe(typeof(TestMessageIdProvider));
	}

	[Fact]
	public void SetStrategyToCustom_WhenWithMessageIdProviderIsCalled()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		_ = config.WithMessageIdProvider<TestMessageIdProvider>();
		var settings = config.Build();

		// Assert
		settings.Strategy.ShouldBe(MessageIdStrategy.Custom);
	}

	[Fact]
	public void DefaultToNullProviderType_WhenNotConfigured()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var settings = config.Build();

		// Assert
		settings.MessageIdProviderType.ShouldBeNull();
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void SupportFluentChaining_WhenMultipleSettingsAreConfigured()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		var result = config
			.WithRetention(TimeSpan.FromHours(12))
			.UseInMemory()
			.WithStrategy(MessageIdStrategy.CompositeKey)
			.WithHeaderName("CustomId");

		// Assert
		result.ShouldBeSameAs(config);
	}

	[Fact]
	public void ApplyAllSettings_WhenFluentChainIsUsed()
	{
		// Arrange
		var config = new InboxHandlerConfiguration();

		// Act
		_ = config
			.WithRetention(TimeSpan.FromHours(12))
			.UseInMemory()
			.WithStrategy(MessageIdStrategy.CompositeKey)
			.WithHeaderName("CustomId");
		var settings = config.Build();

		// Assert
		settings.Retention.ShouldBe(TimeSpan.FromHours(12));
		settings.UseInMemory.ShouldBeTrue();
		settings.Strategy.ShouldBe(MessageIdStrategy.CompositeKey);
		settings.HeaderName.ShouldBe("CustomId");
	}

	#endregion

	#region Test Fixtures

	private sealed class TestMessageIdProvider : IMessageIdProvider
	{
		public string? GetMessageId(IDispatchMessage message, IMessageContext context)
			=> Guid.NewGuid().ToString();
	}

	#endregion
}
