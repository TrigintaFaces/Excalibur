// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions.Tests.Transport;

/// <summary>
/// Unit tests for <see cref="IMessageBusOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class IMessageBusOptionsShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new TestMessageBusOptions();

		// Assert
		options.EnableEncryption.ShouldBeFalse();
		options.EncryptionProviderKey.ShouldBeNull();
		options.EnableRetries.ShouldBeFalse();
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryStrategy.ShouldBe(RetryStrategy.FixedDelay);
		options.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.JitterFactor.ShouldBe(0);
		options.TargetUri.ShouldBeNull();
		options.EnableTelemetry.ShouldBeTrue();
	}

	[Fact]
	public void Name_CanBeSet()
	{
		// Act
		var options = new TestMessageBusOptions { Name = "my-bus" };

		// Assert
		options.Name.ShouldBe("my-bus");
	}

	[Fact]
	public void EnableEncryption_CanBeSet()
	{
		// Act
		var options = new TestMessageBusOptions { EnableEncryption = true };

		// Assert
		options.EnableEncryption.ShouldBeTrue();
	}

	[Fact]
	public void EncryptionProviderKey_CanBeSet()
	{
		// Act
		var options = new TestMessageBusOptions { EncryptionProviderKey = "aes-256" };

		// Assert
		options.EncryptionProviderKey.ShouldBe("aes-256");
	}

	[Fact]
	public void RetrySettings_CanBeConfigured()
	{
		// Act
		var options = new TestMessageBusOptions
		{
			EnableRetries = true,
			MaxRetryAttempts = 5,
			RetryStrategy = RetryStrategy.ExponentialBackoff,
			RetryDelay = TimeSpan.FromSeconds(1),
			JitterFactor = 0.2,
		};

		// Assert
		options.EnableRetries.ShouldBeTrue();
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryStrategy.ShouldBe(RetryStrategy.ExponentialBackoff);
		options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.JitterFactor.ShouldBe(0.2);
	}

	[Fact]
	public void TargetUri_CanBeSet()
	{
		// Arrange
		var uri = new Uri("https://remote-bus.example.com");

		// Act
		var options = new TestMessageBusOptions { TargetUri = uri };

		// Assert
		options.TargetUri.ShouldBe(uri);
	}

	[Fact]
	public void EnableTelemetry_CanBeDisabled()
	{
		// Act
		var options = new TestMessageBusOptions { EnableTelemetry = false };

		// Assert
		options.EnableTelemetry.ShouldBeFalse();
	}

	/// <summary>
	/// Concrete test implementation since IMessageBusOptions is abstract.
	/// </summary>
	private sealed class TestMessageBusOptions : IMessageBusOptions;
}
