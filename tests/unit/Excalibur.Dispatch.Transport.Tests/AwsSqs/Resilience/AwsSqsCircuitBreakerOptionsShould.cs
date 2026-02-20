// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AwsSqs;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsSqsCircuitBreakerOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AwsSqsCircuitBreakerOptions();

		// Assert
		options.FailureThreshold.ShouldBe(5);
		options.BreakDuration.ShouldBe(TimeSpan.FromSeconds(30));
		options.SamplingDuration.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AwsSqsCircuitBreakerOptions
		{
			FailureThreshold = 10,
			BreakDuration = TimeSpan.FromMinutes(1),
			SamplingDuration = TimeSpan.FromMinutes(2),
		};

		// Assert
		options.FailureThreshold.ShouldBe(10);
		options.BreakDuration.ShouldBe(TimeSpan.FromMinutes(1));
		options.SamplingDuration.ShouldBe(TimeSpan.FromMinutes(2));
	}
}
