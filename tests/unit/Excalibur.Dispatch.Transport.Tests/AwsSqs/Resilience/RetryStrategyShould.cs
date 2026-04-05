// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AwsSqs;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Resilience;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class SqsRetryStrategyShould
{
	[Fact]
	public void HaveCorrectEnumValues()
	{
		// Assert
		((int)SqsRetryStrategy.Exponential).ShouldBe(0);
		((int)SqsRetryStrategy.Linear).ShouldBe(1);
		((int)SqsRetryStrategy.Fixed).ShouldBe(2);
	}

	[Fact]
	public void DefaultToExponential()
	{
		// Arrange & Act
		var strategy = default(SqsRetryStrategy);

		// Assert
		strategy.ShouldBe(SqsRetryStrategy.Exponential);
	}
}
