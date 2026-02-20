// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GoogleProviderOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new GoogleProviderOptions();

		// Assert
		options.ProjectId.ShouldBe(string.Empty);
		options.UseEmulator.ShouldBeFalse();
		options.EmulatorHost.ShouldBe("localhost:8085");
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		options.ValidateOnStartup.ShouldBeTrue();
		options.MaxMessages.ShouldBe(100);
		options.AckDeadline.ShouldBe(TimeSpan.FromSeconds(30));
		options.EnableExactlyOnceDelivery.ShouldBeFalse();
		options.EnableMessageOrdering.ShouldBeFalse();
		options.AutoCreateResources.ShouldBeTrue();
		options.FlowControl.ShouldNotBeNull();
		options.PubSubRetryOptions.ShouldNotBeNull();
	}

	[Fact]
	public void AllowSettingProjectAndEmulatorConfiguration()
	{
		// Arrange & Act
		var options = new GoogleProviderOptions
		{
			ProjectId = "my-gcp-project",
			UseEmulator = true,
			EmulatorHost = "pubsub-emulator:8085",
		};

		// Assert
		options.ProjectId.ShouldBe("my-gcp-project");
		options.UseEmulator.ShouldBeTrue();
		options.EmulatorHost.ShouldBe("pubsub-emulator:8085");
	}

	[Fact]
	public void AllowSettingDeliveryConfiguration()
	{
		// Arrange & Act
		var options = new GoogleProviderOptions
		{
			MaxMessages = 500,
			AckDeadline = TimeSpan.FromMinutes(2),
			EnableExactlyOnceDelivery = true,
			EnableMessageOrdering = true,
			AutoCreateResources = false,
		};

		// Assert
		options.MaxMessages.ShouldBe(500);
		options.AckDeadline.ShouldBe(TimeSpan.FromMinutes(2));
		options.EnableExactlyOnceDelivery.ShouldBeTrue();
		options.EnableMessageOrdering.ShouldBeTrue();
		options.AutoCreateResources.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingTimeoutAndValidation()
	{
		// Arrange & Act
		var options = new GoogleProviderOptions
		{
			RequestTimeout = TimeSpan.FromSeconds(120),
			ValidateOnStartup = false,
		};

		// Assert
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(120));
		options.ValidateOnStartup.ShouldBeFalse();
	}
}
