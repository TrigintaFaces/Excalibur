// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.DeadLetter;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DeadLetterEnumsShould
{
	[Theory]
	[InlineData(BackoffType.Constant, 0)]
	[InlineData(BackoffType.Linear, 1)]
	[InlineData(BackoffType.Exponential, 2)]
	[InlineData(BackoffType.DecorrelatedJitter, 3)]
	public void BackoffTypeHaveCorrectValues(BackoffType type, int expected)
	{
		((int)type).ShouldBe(expected);
	}

	[Fact]
	public void BackoffTypeDefaultToConstant()
	{
		default(BackoffType).ShouldBe(BackoffType.Constant);
	}

	[Theory]
	[InlineData(RecommendedAction.Retry, 0)]
	[InlineData(RecommendedAction.DeadLetter, 1)]
	[InlineData(RecommendedAction.Quarantine, 2)]
	[InlineData(RecommendedAction.Skip, 3)]
	public void RecommendedActionHaveCorrectValues(RecommendedAction action, int expected)
	{
		((int)action).ShouldBe(expected);
	}

	[Fact]
	public void RecommendedActionDefaultToRetry()
	{
		default(RecommendedAction).ShouldBe(RecommendedAction.Retry);
	}
}
