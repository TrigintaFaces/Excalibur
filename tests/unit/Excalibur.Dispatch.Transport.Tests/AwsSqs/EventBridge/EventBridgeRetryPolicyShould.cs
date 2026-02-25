// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.EventBridge;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class EventBridgeRetryPolicyShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var policy = new EventBridgeRetryPolicy();

		// Assert
		policy.MaximumRetryAttempts.ShouldBe(2);
		policy.MaximumEventAge.ShouldBe(3600);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var policy = new EventBridgeRetryPolicy
		{
			MaximumRetryAttempts = 5,
			MaximumEventAge = 7200,
		};

		// Assert
		policy.MaximumRetryAttempts.ShouldBe(5);
		policy.MaximumEventAge.ShouldBe(7200);
	}
}
