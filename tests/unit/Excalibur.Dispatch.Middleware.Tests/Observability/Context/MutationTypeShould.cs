// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class MutationTypeShould
{
	[Fact]
	public void HaveExpectedValues()
	{
		((int)MutationType.Added).ShouldBe(0);
		((int)MutationType.Removed).ShouldBe(1);
		((int)MutationType.Modified).ShouldBe(2);
	}

	[Fact]
	public void HaveExactlyThreeMembers()
	{
		Enum.GetValues<MutationType>().Length.ShouldBe(3);
	}
}
