// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization.Grants;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Tests.A3.Grants;

/// <summary>
/// Unit tests for <see cref="GrantQuery"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class GrantQueryShould : UnitTestBase
{
	[Fact]
	public void ImplementIAggregateQuery()
	{
		// Arrange & Act
		var query = new GrantQuery();

		// Assert
		query.ShouldBeAssignableTo<IAggregateQuery<Grant>>();
	}

	[Fact]
	public void BeInstantiable()
	{
		// Act
		var query = new GrantQuery();

		// Assert
		query.ShouldNotBeNull();
	}

	[Fact]
	public void HaveDefaultConstructor()
	{
		// Act
		var query = new GrantQuery();

		// Assert
		query.ShouldNotBeNull();
	}

	[Fact]
	public void SupportCreatingMultipleInstances()
	{
		// Act
		var query1 = new GrantQuery();
		var query2 = new GrantQuery();

		// Assert
		query1.ShouldNotBeSameAs(query2);
	}
}
