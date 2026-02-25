// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SecurityModeShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		SecurityMode.Permissive.ShouldBe((SecurityMode)0);
		SecurityMode.Standard.ShouldBe((SecurityMode)1);
		SecurityMode.Strict.ShouldBe((SecurityMode)2);
		SecurityMode.Compliance.ShouldBe((SecurityMode)3);
	}

	[Fact]
	public void HaveExactlyFourMembers()
	{
		Enum.GetValues<SecurityMode>().Length.ShouldBe(4);
	}
}
