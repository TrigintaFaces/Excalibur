// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data;
using Excalibur.Data.Postgres.Authorization;

namespace Excalibur.Data.Tests.Postgres.Authorization;

/// <summary>
/// Unit tests for <see cref="PostgresActivityGroupStore"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class PostgresActivityGroupStoreShould
{
	private readonly IDomainDb _domainDb = A.Fake<IDomainDb>();

	public PostgresActivityGroupStoreShould()
	{
		A.CallTo(() => _domainDb.Connection).Returns(A.Fake<IDbConnection>());
	}

	[Fact]
	public void ThrowArgumentNullException_WhenDomainDbIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new PostgresActivityGroupStore(null!));
	}

	[Fact]
	public void ImplementIActivityGroupStore()
	{
		var store = new PostgresActivityGroupStore(_domainDb);
		store.ShouldBeAssignableTo<IActivityGroupStore>();
	}

	[Fact]
	public void ReturnNull_WhenGetServiceRequestsAnyType()
	{
		var store = new PostgresActivityGroupStore(_domainDb);
		store.GetService(typeof(IActivityGroupGrantStore)).ShouldBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenGetServiceTypeIsNull()
	{
		var store = new PostgresActivityGroupStore(_domainDb);
		Should.Throw<ArgumentNullException>(() => store.GetService(null!));
	}
}
