// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Common;

[Trait(TraitNames.Category, TestCategories.Unit)]
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
		options.Emulator.UseEmulator.ShouldBeFalse();
		options.Emulator.EmulatorHost.ShouldBe("localhost:8085");
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		options.ValidateOnStartup.ShouldBeTrue();
		options.Subscription.MaxMessages.ShouldBe(100);
		options.Subscription.AckDeadline.ShouldBe(TimeSpan.FromSeconds(30));
		options.Subscription.EnableExactlyOnceDelivery.ShouldBeFalse();
		options.Subscription.EnableMessageOrdering.ShouldBeFalse();
		options.Subscription.AutoCreateResources.ShouldBeTrue();
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
			Emulator =
			{
				UseEmulator = true,
				EmulatorHost = "pubsub-emulator:8085",
			},
		};

		// Assert
		options.ProjectId.ShouldBe("my-gcp-project");
		options.Emulator.UseEmulator.ShouldBeTrue();
		options.Emulator.EmulatorHost.ShouldBe("pubsub-emulator:8085");
	}

	[Fact]
	public void AllowSettingDeliveryConfiguration()
	{
		// Arrange & Act
		var options = new GoogleProviderOptions
		{
			Subscription =
			{
				MaxMessages = 500,
				AckDeadline = TimeSpan.FromMinutes(2),
				EnableExactlyOnceDelivery = true,
				EnableMessageOrdering = true,
				AutoCreateResources = false,
			},
		};

		// Assert
		options.Subscription.MaxMessages.ShouldBe(500);
		options.Subscription.AckDeadline.ShouldBe(TimeSpan.FromMinutes(2));
		options.Subscription.EnableExactlyOnceDelivery.ShouldBeTrue();
		options.Subscription.EnableMessageOrdering.ShouldBeTrue();
		options.Subscription.AutoCreateResources.ShouldBeFalse();
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
