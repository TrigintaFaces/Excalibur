// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SubscriptionStartPositionShould
{
	[Fact]
	public void DefineBeginningValue()
	{
		((int)SubscriptionStartPosition.Beginning).ShouldBe(0);
	}

	[Fact]
	public void DefineEndValue()
	{
		((int)SubscriptionStartPosition.End).ShouldBe(1);
	}

	[Fact]
	public void DefinePositionValue()
	{
		((int)SubscriptionStartPosition.Position).ShouldBe(2);
	}

	[Fact]
	public void HaveExactlyThreeValues()
	{
		Enum.GetValues<SubscriptionStartPosition>().Length.ShouldBe(3);
	}
}
