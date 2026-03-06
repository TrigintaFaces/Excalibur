// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data;
using Excalibur.Data.SqlServer.Authorization;

namespace Excalibur.Data.Tests.SqlServer.Authorization;

/// <summary>
/// Unit tests for <see cref="SqlServerGrantStore"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class SqlServerGrantStoreShould
{
	private readonly IDomainDb _domainDb = A.Fake<IDomainDb>();

	public SqlServerGrantStoreShould()
	{
		A.CallTo(() => _domainDb.Connection).Returns(A.Fake<IDbConnection>());
	}

	[Fact]
	public void ThrowArgumentNullException_WhenDomainDbIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new SqlServerGrantStore(null!));
	}

	[Fact]
	public void ImplementIGrantStore()
	{
		// Arrange & Act
		var store = new SqlServerGrantStore(_domainDb);

		// Assert
		store.ShouldBeAssignableTo<IGrantStore>();
	}

	[Fact]
	public void ImplementIGrantQueryStore()
	{
		// Arrange & Act
		var store = new SqlServerGrantStore(_domainDb);

		// Assert
		store.ShouldBeAssignableTo<IGrantQueryStore>();
	}

	[Fact]
	public void ImplementIActivityGroupGrantStore()
	{
		// Arrange & Act
		var store = new SqlServerGrantStore(_domainDb);

		// Assert
		store.ShouldBeAssignableTo<IActivityGroupGrantStore>();
	}

	[Fact]
	public void ReturnSelf_WhenGetServiceRequestsIGrantQueryStore()
	{
		// Arrange
		var store = new SqlServerGrantStore(_domainDb);

		// Act
		var result = store.GetService(typeof(IGrantQueryStore));

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeSameAs(store);
	}

	[Fact]
	public void ReturnSelf_WhenGetServiceRequestsIActivityGroupGrantStore()
	{
		// Arrange
		var store = new SqlServerGrantStore(_domainDb);

		// Act
		var result = store.GetService(typeof(IActivityGroupGrantStore));

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeSameAs(store);
	}

	[Fact]
	public void ReturnNull_WhenGetServiceRequestsUnsupportedType()
	{
		// Arrange
		var store = new SqlServerGrantStore(_domainDb);

		// Act
		var result = store.GetService(typeof(IDisposable));

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenGetServiceTypeIsNull()
	{
		// Arrange
		var store = new SqlServerGrantStore(_domainDb);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => store.GetService(null!));
	}
}
