// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data;
using Excalibur.Data.Postgres.Authorization;

namespace Excalibur.Data.Tests.Postgres.Authorization;

/// <summary>
/// Unit tests for <see cref="PostgresGrantStore"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class PostgresGrantStoreShould
{
	private readonly IDomainDb _domainDb = A.Fake<IDomainDb>();

	public PostgresGrantStoreShould()
	{
		A.CallTo(() => _domainDb.Connection).Returns(A.Fake<IDbConnection>());
	}

	[Fact]
	public void ThrowArgumentNullException_WhenDomainDbIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new PostgresGrantStore(null!));
	}

	[Fact]
	public void ImplementIGrantStore()
	{
		var store = new PostgresGrantStore(_domainDb);
		store.ShouldBeAssignableTo<IGrantStore>();
	}

	[Fact]
	public void ImplementIGrantQueryStore()
	{
		var store = new PostgresGrantStore(_domainDb);
		store.ShouldBeAssignableTo<IGrantQueryStore>();
	}

	[Fact]
	public void ImplementIActivityGroupGrantStore()
	{
		var store = new PostgresGrantStore(_domainDb);
		store.ShouldBeAssignableTo<IActivityGroupGrantStore>();
	}

	[Fact]
	public void ReturnSelf_WhenGetServiceRequestsIGrantQueryStore()
	{
		var store = new PostgresGrantStore(_domainDb);
		var result = store.GetService(typeof(IGrantQueryStore));

		result.ShouldNotBeNull();
		result.ShouldBeSameAs(store);
	}

	[Fact]
	public void ReturnSelf_WhenGetServiceRequestsIActivityGroupGrantStore()
	{
		var store = new PostgresGrantStore(_domainDb);
		var result = store.GetService(typeof(IActivityGroupGrantStore));

		result.ShouldNotBeNull();
		result.ShouldBeSameAs(store);
	}

	[Fact]
	public void ReturnNull_WhenGetServiceRequestsUnsupportedType()
	{
		var store = new PostgresGrantStore(_domainDb);
		store.GetService(typeof(IDisposable)).ShouldBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenGetServiceTypeIsNull()
	{
		var store = new PostgresGrantStore(_domainDb);
		Should.Throw<ArgumentNullException>(() => store.GetService(null!));
	}
}
