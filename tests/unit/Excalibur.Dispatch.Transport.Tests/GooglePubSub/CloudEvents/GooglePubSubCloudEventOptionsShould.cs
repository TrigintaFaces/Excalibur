// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.CloudEvents;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class GooglePubSubCloudEventOptionsShould
{
	[Fact]
	public void RetryPolicyHaveCorrectDefaults()
	{
		// Arrange & Act
		var policy = new GooglePubSubRetryPolicy();

		// Assert
		policy.MaxRetryAttempts.ShouldBe(3);
		policy.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		policy.MaxDelay.ShouldBe(TimeSpan.FromSeconds(60));
		policy.DelayMultiplier.ShouldBe(2.0);
		policy.UseJitter.ShouldBeTrue();
	}

	[Fact]
	public void RetryPolicyAllowSettingAllProperties()
	{
		// Arrange & Act
		var policy = new GooglePubSubRetryPolicy
		{
			MaxRetryAttempts = 5,
			InitialDelay = TimeSpan.FromMilliseconds(200),
			MaxDelay = TimeSpan.FromSeconds(120),
			DelayMultiplier = 3.0,
			UseJitter = false,
		};

		// Assert
		policy.MaxRetryAttempts.ShouldBe(5);
		policy.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
		policy.MaxDelay.ShouldBe(TimeSpan.FromSeconds(120));
		policy.DelayMultiplier.ShouldBe(3.0);
		policy.UseJitter.ShouldBeFalse();
	}
}
