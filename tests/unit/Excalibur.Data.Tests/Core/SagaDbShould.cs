// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaDbShould
{
	[Fact]
	public void CreateInstanceWithConnection()
	{
		var connection = A.Fake<IDbConnection>();
		var db = new SagaDb(connection);
		db.ShouldNotBeNull();
		db.ShouldBeAssignableTo<ISagaDb>();
	}

	[Fact]
	public void ThrowForNullConnection()
	{
		Should.Throw<ArgumentNullException>(() => new SagaDb(null!));
	}
}
