// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.PubSub;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class GooglePubSubEnumsShould
{
	[Theory]
	[InlineData(DeadLetterAction.Acknowledge, 0)]
	[InlineData(DeadLetterAction.Reject, 1)]
	[InlineData(DeadLetterAction.Retry, 2)]
	[InlineData(DeadLetterAction.Archive, 3)]
	public void HaveCorrectDeadLetterActionValues(DeadLetterAction action, int expected)
	{
		((int)action).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllDeadLetterActionMembers()
	{
		Enum.GetValues<DeadLetterAction>().Length.ShouldBe(4);
	}

	[Theory]
	[InlineData(PoisonReason.MaxDeliveryAttemptsExceeded, 0)]
	[InlineData(PoisonReason.ProcessingTimeout, 1)]
	[InlineData(PoisonReason.UnhandledException, 2)]
	[InlineData(PoisonReason.InvalidFormat, 3)]
	[InlineData(PoisonReason.MessageExpired, 4)]
	[InlineData(PoisonReason.Unknown, 99)]
	public void HaveCorrectPoisonReasonValues(PoisonReason reason, int expected)
	{
		((int)reason).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllPoisonReasonMembers()
	{
		Enum.GetValues<PoisonReason>().Length.ShouldBe(6);
	}
}
