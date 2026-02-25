// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.Grants;

/// <summary>
/// Unit tests for <see cref="ExistsGrantRequest"/>.
/// Covers SQL structure, parameter setup, and expiration check.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class ExistsGrantRequestShould
{
	private const string UserId = "user-123";
	private const string TenantId = "tenant-1";
	private const string GrantType = "role";
	private const string Qualifier = "admin";

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Act
		var request = new ExistsGrantRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(ExistsGrantRequest));
	}

	[Fact]
	public void GenerateSql_WithSelectExists()
	{
		// Act
		var request = new ExistsGrantRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("SELECT EXISTS");
	}

	[Fact]
	public void GenerateSql_QueryingGrantTable()
	{
		// Act
		var request = new ExistsGrantRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("authz.grant");
	}

	[Fact]
	public void GenerateSql_WithWhereClauseForAllIdentifiers()
	{
		// Act
		var request = new ExistsGrantRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("user_id = @UserId");
		sql.ShouldContain("tenant_id = @TenantId");
		sql.ShouldContain("grant_type = @GrantType");
		sql.ShouldContain("qualifier = @Qualifier");
	}

	[Fact]
	public void GenerateSql_WithExpirationCheck()
	{
		// Act
		var request = new ExistsGrantRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert - Only returns non-expired grants
		var sql = request.Command.CommandText;
		sql.ShouldContain("COALESCE(expires_on, 'infinity')");
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Act
		var request = new ExistsGrantRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}
}
