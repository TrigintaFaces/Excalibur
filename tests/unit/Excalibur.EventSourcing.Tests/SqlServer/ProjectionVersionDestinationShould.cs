// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.SqlServer;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>A read model declaring no generation. Stands in for every projection that exists today.</summary>
public sealed class UnversionedOrderView
{
	/// <summary>Gets or sets the row id.</summary>
	public string Id { get; set; } = string.Empty;
}

/// <summary>The same read model, labelled as the generation it already is.</summary>
[ProjectionVersion(1)]
public sealed class FirstGenerationOrderView
{
	/// <summary>Gets or sets the row id.</summary>
	public string Id { get; set; } = string.Empty;
}

/// <summary>The next generation of that read model.</summary>
[ProjectionVersion(2)]
public sealed class SecondGenerationOrderView
{
	/// <summary>Gets or sets the row id.</summary>
	public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Binds a declared projection generation to the SQL Server table a store addresses, and to the
/// destination identity it advertises.
/// </summary>
/// <remarks>
/// Both the qualified table every statement interpolates and the identity are composed from one resolved
/// table-name local, so asserting the identity asserts the table: they are one variable, not two
/// derivations that could disagree.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServer")]
[Trait("Database", "SqlServer")]
public sealed class ProjectionVersionDestinationShould : UnitTestBase
{
	private const string ConnectionString = "Server=localhost;Database=TestDb;TrustServerCertificate=True";

	// The identity a SQL Server projection resolves to with the default schema, a default table name and
	// no declared generation. HARD-CODED ON PURPOSE: deriving it through the convention under test would
	// make the arm agree with whatever that convention currently does, which is precisely the drift it
	// exists to catch.
	private const string UnversionedOrderIdentity = "sqlserver/dbo/UnversionedOrderView";

	/// <summary>
	/// LIVENESS, and the arm that matters most: a projection declaring no generation keeps the exact
	/// destination it addresses today. Without it, a design that versions everything satisfies every
	/// safety arm below while silently re-folding every existing read model from zero.
	/// </summary>
	[Fact]
	public void ResolveTheUnchangedTableAndIdentityForAProjectionDeclaringNoGeneration()
	{
		IdentityOf<UnversionedOrderView>().ShouldBe(UnversionedOrderIdentity);
		IdentityOf<UnversionedOrderView>(tableName: "Orders").ShouldBe("sqlserver/dbo/Orders");

		// The first generation IS the unversioned destination, so an existing read model can adopt the
		// declaration without moving.
		IdentityOf<FirstGenerationOrderView>().ShouldBe("sqlserver/dbo/FirstGenerationOrderView");
		IdentityOf<FirstGenerationOrderView>(tableName: "Orders").ShouldBe("sqlserver/dbo/Orders");
	}

	/// <summary>SAFETY. A bump moves the destination: a new table, and therefore a new identity.</summary>
	[Fact]
	public void ResolveADifferentTableAndIdentityWhenTheDeclaredGenerationIsBumped()
	{
		IdentityOf<SecondGenerationOrderView>().ShouldBe("sqlserver/dbo/SecondGenerationOrderView_v2");
		IdentityOf<SecondGenerationOrderView>(tableName: "Orders").ShouldBe("sqlserver/dbo/Orders_v2");

		// The qualified table every statement interpolates moves with it, not just the advertised
		// identity -- a store that versioned only the identity would pass an identity-only assertion
		// while writing to the previous generation's table.
		QualifiedTableOf<SecondGenerationOrderView>(tableName: "Orders").ShouldBe("[dbo].[Orders_v2]");
		QualifiedTableOf<FirstGenerationOrderView>(tableName: "Orders").ShouldBe("[dbo].[Orders]");
	}

	/// <summary>
	/// SAFETY. Two generations configured onto one table name are two destinations, so the previous
	/// generation keeps its rows and keeps serving reads while the next folds.
	/// </summary>
	[Fact]
	public void ResolveTwoGenerationsOfOneReadModelAsDistinctCoexistingDestinations()
	{
		var first = IdentityOf<FirstGenerationOrderView>(tableName: "Orders");
		var second = IdentityOf<SecondGenerationOrderView>(tableName: "Orders");

		first.ShouldNotBe(second);
		first.ShouldBe("sqlserver/dbo/Orders");
		second.ShouldBe("sqlserver/dbo/Orders_v2");
	}

	/// <summary>
	/// The generation segment uses the separator a SQL identifier permits, not the hyphen a search index
	/// uses, so the composed table name survives the identifier check this store applies before
	/// composing. A hyphen would be rejected by that check.
	/// </summary>
	[Fact]
	public void ComposeTheGenerationSegmentAsALegalSqlIdentifier()
	{
		var identity = IdentityOf<SecondGenerationOrderView>(tableName: "Orders");

		identity.ShouldBe("sqlserver/dbo/Orders_v2");
		identity.ShouldNotContain("-v2");
		identity.Split('/')[2].ShouldMatch("^[A-Za-z0-9_]+$");
	}

	/// <summary>A declared generation travels with a non-default schema rather than replacing it.</summary>
	[Fact]
	public void KeepTheConfiguredSchemaWhenTheGenerationParticipatesInTheTable()
	{
		IdentityOf<SecondGenerationOrderView>(tableName: "Orders", schemaName: "Reporting")
			.ShouldBe("sqlserver/Reporting/Orders_v2");
		QualifiedTableOf<SecondGenerationOrderView>(tableName: "Orders", schemaName: "Reporting")
			.ShouldBe("[Reporting].[Orders_v2]");
	}

	private static SqlServerProjectionStore<TProjection> StoreOf<TProjection>(
		string? tableName = null,
		string schemaName = "dbo")
		where TProjection : class => new(
			ConnectionString,
			NullLoggerFactory.CreateLogger<SqlServerProjectionStore<TProjection>>(),
			TestTenantContext.SingleTenantDefault,
			tableName: tableName,
			jsonOptions: null,
			schemaName: schemaName);

	private static string IdentityOf<TProjection>(string? tableName = null, string schemaName = "dbo")
		where TProjection : class
	{
		var store = StoreOf<TProjection>(tableName, schemaName);

		var identity = (IProjectionDestinationIdentity)((IServiceProvider)store)
			.GetService(typeof(IProjectionDestinationIdentity))!;

		// The capability must be a distinct object rather than the store answering through a default
		// interface implementation, or this arm would pass for a store that resolves no identity at all.
		(identity is IProjectionStore<TProjection>).ShouldBeFalse();
		return identity.Identity;
	}

	private static string QualifiedTableOf<TProjection>(string? tableName = null, string schemaName = "dbo")
		where TProjection : class => (string)typeof(SqlServerProjectionStore<TProjection>)
			.GetField("_qualifiedTable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.GetValue(StoreOf<TProjection>(tableName, schemaName))!;
}
