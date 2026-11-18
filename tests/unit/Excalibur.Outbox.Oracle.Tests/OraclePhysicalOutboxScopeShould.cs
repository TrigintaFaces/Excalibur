// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Shouldly;

namespace Excalibur.Outbox.Oracle.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class OraclePhysicalOutboxScopeShould
{
	[Fact]
	public void PreserveTheTwoIdentityComponentsWithoutDelimiterAmbiguity()
	{
		OraclePhysicalOutboxScope.Encode("A:B", "C").ShouldNotBe(OraclePhysicalOutboxScope.Encode("A", "B:C"));
		OraclePhysicalOutboxScope.Encode("owner", "table").ShouldNotBe(OraclePhysicalOutboxScope.Encode("OWNER", "TABLE"));
		OraclePhysicalOutboxScope.Encode("owner", "table").ShouldBe(OraclePhysicalOutboxScope.Encode("owner", "table"));
	}

	[Fact]
	public void RefuseOversizedUnicodeIdentityBeforeAnyDatabaseMutation()
	{
		Should.Throw<InvalidOperationException>(() => OraclePhysicalOutboxScope.Encode(new string('\u4e00', 128), new string('\u4e00', 128)))
			.Message.ShouldContain("600-byte");
	}
}
