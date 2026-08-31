// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.OrderingKey;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class OrderingKeyOptionsShould
{
	[Fact]
	public void ValidateThrowWhenMaxConcurrentOrderingKeysZero()
	{
		// Arrange
		var options = new OrderingKeyOptions { MaxConcurrentOrderingKeys = 0 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("MaxConcurrentOrderingKeys");
	}

	[Fact]
	public void ValidateThrowWhenMaxMessagesPerOrderingKeyZero()
	{
		// Arrange
		var options = new OrderingKeyOptions { MaxMessagesPerOrderingKey = 0 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("MaxMessagesPerOrderingKey");
	}

	[Fact]
	public void ValidateThrowWhenMaxRetriesNegative()
	{
		// Arrange
		var options = new OrderingKeyOptions { MaxRetryAttempts = -1 };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("MaxRetryAttempts");
	}

	[Fact]
	public void ValidateThrowWhenRetryDelayNegative()
	{
		// Arrange
		var options = new OrderingKeyOptions { RetryDelay = TimeSpan.FromSeconds(-1) };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate())
			.Message.ShouldContain("RetryDelay");
	}

	[Fact]
	public void ValidateSucceedWithValidConfig()
	{
		// Arrange
		var options = new OrderingKeyOptions();

		// Act & Assert — should not throw
		options.Validate();
	}
}
