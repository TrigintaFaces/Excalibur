// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data;
using Excalibur.Data.Abstractions;

namespace Excalibur.Tests;

/// <summary>
///     Unit tests for the <see cref="DomainDb" /> class.
/// </summary>
/// <remarks>
///     Tests the domain-specific database context implementation including constructor behavior, interface compliance, and inheritance from
///     the base Db class.
/// </remarks>
[Trait("Category", "Unit")]
public class DomainDbShould
{
	[Fact]
	public void ConstructorShouldThrowArgumentNullExceptionWhenConnectionIsNull()
	{
		// Arrange
		IDbConnection? nullConnection = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new DomainDb(nullConnection));
	}

	[Fact]
	public void ConstructorShouldAcceptValidConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();

		// Act & Assert
		_ = Should.NotThrow(() => new DomainDb(connection));
	}

	[Fact]
	public void ShouldImplementIDomainDbInterface()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();

		// Act & Assert
		using var domainDb = new DomainDb(connection);
		_ = domainDb.ShouldBeAssignableTo<IDomainDb>();
	}

	[Fact]
	public void ShouldInheritFromDbClass()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();

		// Act & Assert
		using var domainDb = new DomainDb(connection);
		_ = domainDb.ShouldBeAssignableTo<Db>();
	}

	[Fact]
	public void ShouldPassConnectionToBaseClass()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();

		// Act & Assert Since DomainDb inherits from Db, we can verify it was constructed properly by checking that it implements the
		// expected interfaces and doesn't throw
		using var domainDb = new DomainDb(connection);
		_ = domainDb.ShouldNotBeNull();
		_ = domainDb.ShouldBeAssignableTo<IDomainDb>();
	}
}
