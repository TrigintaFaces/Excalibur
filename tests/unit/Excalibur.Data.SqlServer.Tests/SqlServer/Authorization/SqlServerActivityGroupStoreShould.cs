// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data;
using Excalibur.Data.SqlServer.Authorization;

namespace Excalibur.Data.Tests.SqlServer.Authorization;

/// <summary>
/// Unit tests for <see cref="SqlServerActivityGroupStore"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "A3")]
public sealed class SqlServerActivityGroupStoreShould
{
	private readonly IDomainDb _domainDb = A.Fake<IDomainDb>();

	public SqlServerActivityGroupStoreShould()
	{
		A.CallTo(() => _domainDb.Connection).Returns(A.Fake<IDbConnection>());
	}

	[Fact]
	public void ThrowArgumentNullException_WhenDomainDbIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new SqlServerActivityGroupStore(null!));
	}

	[Fact]
	public void ImplementIActivityGroupStore()
	{
		// Arrange & Act
		var store = new SqlServerActivityGroupStore(_domainDb);

		// Assert
		store.ShouldBeAssignableTo<IActivityGroupStore>();
	}

	[Fact]
	public void ReturnNull_WhenGetServiceRequestsAnyType()
	{
		// Arrange -- SqlServerActivityGroupStore does not implement any sub-interfaces
		var store = new SqlServerActivityGroupStore(_domainDb);

		// Act
		var result = store.GetService(typeof(IActivityGroupGrantStore));

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenGetServiceTypeIsNull()
	{
		// Arrange
		var store = new SqlServerActivityGroupStore(_domainDb);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => store.GetService(null!));
	}
}
