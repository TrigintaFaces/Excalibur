// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
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
		Should.Throw<ArgumentNullException>(() => new SqlServerActivityGroupStore(null!, DefaultOptions));
	}

	[Fact]
	public void ImplementIActivityGroupStore()
	{
		// Arrange & Act
		var store = new SqlServerActivityGroupStore(_domainDb, DefaultOptions);

		// Assert
		store.ShouldBeAssignableTo<IActivityGroupStore>();
	}

	[Fact]
	public void ReturnNull_WhenGetServiceRequestsAnyType()
	{
		// Arrange -- SqlServerActivityGroupStore does not implement any sub-interfaces.
		// GetService is an EXPLICIT default interface implementation on IActivityGroupStore,
		// so it is reachable only through the interface -- which is how a consumer reaches it.
		IServiceProvider store = new SqlServerActivityGroupStore(_domainDb, DefaultOptions);

		// Act
		var result = store.GetService(typeof(IActivityGroupGrantStore));

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenGetServiceTypeIsNull()
	{
		// Arrange
		IServiceProvider store = new SqlServerActivityGroupStore(_domainDb, DefaultOptions);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => store.GetService(null!));
	}
	private static readonly Microsoft.Extensions.Options.IOptions<SqlServerAuthorizationOptions> DefaultOptions =
		Microsoft.Extensions.Options.Options.Create(new SqlServerAuthorizationOptions());

	[Fact]
	public void ThrowWhenOptionsIsNull() =>
		Should.Throw<ArgumentNullException>(() => new SqlServerActivityGroupStore(_domainDb, null!));

	/// <summary>
	/// The schema name is written into every statement, so one that is not a plain identifier is refused at
	/// construction rather than reaching the database.
	/// </summary>
	[Theory]
	[InlineData("authz; DROP TABLE x")]
	[InlineData("auth z")]
	[InlineData("")]
	public void RefuseASchemaNameThatIsNotAnIdentifier(string schema) =>
		Should.Throw<ArgumentException>(() => new SqlServerActivityGroupStore(
			_domainDb,
			Microsoft.Extensions.Options.Options.Create(new SqlServerAuthorizationOptions { SchemaName = schema })));

	/// <summary>The same refusal is reported at startup, through the options validator.</summary>
	[Theory]
	[InlineData("authz; DROP TABLE x")]
	[InlineData("")]
	public void ReportAnInvalidSchemaNameAtStartup(string schema) =>
		new SqlServerAuthorizationOptionsValidator().Validate(null, new SqlServerAuthorizationOptions { SchemaName = schema }).Failed.ShouldBeTrue();

	/// <summary>LIVENESS: the default and a custom identifier are both accepted.</summary>
	[Theory]
	[InlineData("authz")]
	[InlineData("tenant_app_2")]
	public void AcceptAValidSchemaName(string schema)
	{
		new SqlServerAuthorizationOptionsValidator().Validate(null, new SqlServerAuthorizationOptions { SchemaName = schema }).Succeeded.ShouldBeTrue();
		Should.NotThrow(() => new SqlServerActivityGroupStore(
			_domainDb,
			Microsoft.Extensions.Options.Options.Create(new SqlServerAuthorizationOptions { SchemaName = schema })));
	}

	[Fact]
	public void DefaultToTheShippedSchema() => new SqlServerAuthorizationOptions().SchemaName.ShouldBe("authz");

}
