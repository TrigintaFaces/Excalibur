// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Postgres;

/// <summary>A read model declaring no generation. Stands in for every projection that exists today.</summary>
public sealed class UnversionedCustomerView
{
	/// <summary>Gets or sets the row id.</summary>
	public string Id { get; set; } = string.Empty;
}

/// <summary>The same read model, labelled as the generation it already is.</summary>
[ProjectionVersion(1)]
public sealed class FirstGenerationCustomerView
{
	/// <summary>Gets or sets the row id.</summary>
	public string Id { get; set; } = string.Empty;
}

/// <summary>The next generation of that read model.</summary>
[ProjectionVersion(2)]
public sealed class SecondGenerationCustomerView
{
	/// <summary>Gets or sets the row id.</summary>
	public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Binds a declared projection generation to the Postgres table a store addresses, and to the
/// destination identity it advertises.
/// </summary>
/// <remarks>
/// The identity is composed in the constructor from the same resolved table name every statement is
/// qualified with, so asserting the identity asserts the table: they are one variable, not two
/// derivations that could disagree.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class ProjectionVersionDestinationShould
{
	private const string ConnectionString = "Host=localhost;Database=TestDb";

	// The identity a Postgres projection resolves to with a default schema, a default table name and
	// no declared generation. HARD-CODED ON PURPOSE: deriving it through the convention under test
	// would make the arm agree with whatever that convention currently does, which is precisely the
	// drift it exists to catch. SampleProjection predates generations entirely.
	private const string UnversionedSampleIdentity = "postgres/public/sample_projection";
	private const string UnversionedCustomerIdentity = "postgres/public/unversioned_customer_view";

	private static string IdentityOf<TProjection>(string? tableName = null)
		where TProjection : class
	{
		var store = new PostgresProjectionStore<TProjection>(
			connectionString: ConnectionString,
			NullLoggerFactory.Instance.CreateLogger<PostgresProjectionStore<TProjection>>(),
			tenantContext: TestTenantContext.SingleTenantDefault,
			tableName: tableName);

		var identity = (IProjectionDestinationIdentity)((IServiceProvider)store)
			.GetService(typeof(IProjectionDestinationIdentity))!;

		// The capability must be a distinct object rather than the store answering through a default
		// interface implementation, or this arm would pass for a store that resolves no identity at all.
		(identity is IProjectionStore<TProjection>).ShouldBeFalse();
		return identity.Identity;
	}

	/// <summary>
	/// LIVENESS, and the arm that matters most: a projection declaring no generation keeps the exact
	/// destination it addresses today. Without it, a design that versions everything satisfies every
	/// safety arm below while silently re-folding every existing read model from zero.
	/// </summary>
	[Fact]
	public void ResolveTheUnchangedTableAndIdentityForAProjectionDeclaringNoGeneration()
	{
		IdentityOf<SampleProjection>().ShouldBe(UnversionedSampleIdentity);
		IdentityOf<UnversionedCustomerView>().ShouldBe(UnversionedCustomerIdentity);
		IdentityOf<UnversionedCustomerView>(tableName: "customers").ShouldBe("postgres/public/customers");
	}

	/// <summary>
	/// LIVENESS. The first generation is the unversioned destination, so an existing read model can
	/// adopt the declaration without moving.
	/// </summary>
	[Fact]
	public void ResolveTheUnchangedTableAndIdentityForAProjectionDeclaringTheFirstGeneration()
	{
		IdentityOf<FirstGenerationCustomerView>()
			.ShouldBe("postgres/public/first_generation_customer_view");
		IdentityOf<FirstGenerationCustomerView>(tableName: "customers")
			.ShouldBe("postgres/public/customers");
	}

	/// <summary>SAFETY. A bump moves the destination: a new table, and therefore a new identity.</summary>
	[Fact]
	public void ResolveADifferentTableAndIdentityWhenTheDeclaredGenerationIsBumped()
	{
		IdentityOf<SecondGenerationCustomerView>()
			.ShouldBe("postgres/public/second_generation_customer_view_v2");
		IdentityOf<SecondGenerationCustomerView>(tableName: "customers")
			.ShouldBe("postgres/public/customers_v2");
	}

	/// <summary>
	/// SAFETY. Two generations configured onto one table name are two destinations, so the previous
	/// generation keeps its rows and keeps serving reads while the next folds.
	/// </summary>
	[Fact]
	public void ResolveTwoGenerationsOfOneReadModelAsDistinctCoexistingDestinations()
	{
		var first = IdentityOf<FirstGenerationCustomerView>(tableName: "customers");
		var second = IdentityOf<SecondGenerationCustomerView>(tableName: "customers");

		first.ShouldNotBe(second);
		first.ShouldBe("postgres/public/customers");
		second.ShouldBe("postgres/public/customers_v2");
	}

	/// <summary>
	/// The generation segment uses the separator a SQL identifier permits, not the hyphen a search
	/// index uses, so the composed table name survives the identifier check this store applies.
	/// </summary>
	[Fact]
	public void ComposeTheGenerationSegmentAsALegalSqlIdentifier()
	{
		var identity = IdentityOf<SecondGenerationCustomerView>(tableName: "customers");

		identity.ShouldBe("postgres/public/customers_v2");
		identity.ShouldNotContain("-v2");
		identity.Split('/')[2].ShouldMatch("^[A-Za-z0-9_]+$");
	}

	/// <summary>A declared generation travels with a non-default schema rather than replacing it.</summary>
	[Fact]
	public void KeepTheConfiguredSchemaWhenTheGenerationParticipatesInTheTable()
	{
		var store = new PostgresProjectionStore<SecondGenerationCustomerView>(
			connectionString: ConnectionString,
			NullLoggerFactory.Instance.CreateLogger<PostgresProjectionStore<SecondGenerationCustomerView>>(),
			tenantContext: TestTenantContext.SingleTenantDefault,
			tableName: "customers",
			schemaName: "read_models");

		var identity = (IProjectionDestinationIdentity)((IServiceProvider)store)
			.GetService(typeof(IProjectionDestinationIdentity))!;
		identity.Identity.ShouldBe("postgres/read_models/customers_v2");
	}
}
