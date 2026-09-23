// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.Data;
using Excalibur.Data.Postgres.Authorization;

namespace Excalibur.Data.Tests.Postgres.Authorization;

/// <summary>
/// Unit tests for <see cref="PostgresActivityGroupStore"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
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
		Should.Throw<ArgumentNullException>(() => new PostgresActivityGroupStore(null!, DefaultOptions));
	}

	[Fact]
	public void ImplementIActivityGroupStore()
	{
		var store = new PostgresActivityGroupStore(_domainDb, DefaultOptions);
		store.ShouldBeAssignableTo<IActivityGroupStore>();
	}

	[Fact]
	public void ReturnNull_WhenGetServiceRequestsAnyType()
	{
		// GetService is an EXPLICIT default interface implementation on IActivityGroupStore,
		// so it is reachable only through the interface -- which is how a consumer reaches it.
		IServiceProvider store = new PostgresActivityGroupStore(_domainDb, DefaultOptions);
		store.GetService(typeof(IActivityGroupGrantStore)).ShouldBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenGetServiceTypeIsNull()
	{
		IServiceProvider store = new PostgresActivityGroupStore(_domainDb, DefaultOptions);
		Should.Throw<ArgumentNullException>(() => store.GetService(null!));
	}
	private static readonly Microsoft.Extensions.Options.IOptions<PostgresAuthorizationOptions> DefaultOptions =
		Microsoft.Extensions.Options.Options.Create(new PostgresAuthorizationOptions());

	[Fact]
	public void ThrowWhenOptionsIsNull() =>
		Should.Throw<ArgumentNullException>(() => new PostgresActivityGroupStore(_domainDb, null!));

	/// <summary>
	/// The schema name is written into every statement, so one that is not a plain identifier is refused at
	/// construction rather than reaching the database.
	/// </summary>
	[Theory]
	[InlineData("authz; DROP TABLE x")]
	[InlineData("auth z")]
	[InlineData("")]
	public void RefuseASchemaNameThatIsNotAnIdentifier(string schema) =>
		Should.Throw<ArgumentException>(() => new PostgresActivityGroupStore(
			_domainDb,
			Microsoft.Extensions.Options.Options.Create(new PostgresAuthorizationOptions { SchemaName = schema })));

	/// <summary>The same refusal is reported at startup, through the options validator.</summary>
	[Theory]
	[InlineData("authz; DROP TABLE x")]
	[InlineData("")]
	public void ReportAnInvalidSchemaNameAtStartup(string schema) =>
		new PostgresAuthorizationOptionsValidator().Validate(null, new PostgresAuthorizationOptions { SchemaName = schema }).Failed.ShouldBeTrue();

	/// <summary>LIVENESS: the default and a custom identifier are both accepted.</summary>
	[Theory]
	[InlineData("authz")]
	[InlineData("tenant_app_2")]
	public void AcceptAValidSchemaName(string schema)
	{
		new PostgresAuthorizationOptionsValidator().Validate(null, new PostgresAuthorizationOptions { SchemaName = schema }).Succeeded.ShouldBeTrue();
		Should.NotThrow(() => new PostgresActivityGroupStore(
			_domainDb,
			Microsoft.Extensions.Options.Options.Create(new PostgresAuthorizationOptions { SchemaName = schema })));
	}

	[Fact]
	public void DefaultToTheShippedSchema() => new PostgresAuthorizationOptions().SchemaName.ShouldBe("authz");

}
