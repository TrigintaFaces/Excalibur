// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.Grants;

/// <summary>
/// Unit tests for <see cref="MatchingGrantsRequest"/>.
/// Covers SQL structure, parameter setup, and wildcard matching.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class MatchingGrantsRequestShould
{
	private const string UserId = "user-123";
	private const string TenantId = "tenant-%";
	private const string GrantType = "role";
	private const string Qualifier = "%";

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Act
		var request = new MatchingGrantsRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(MatchingGrantsRequest));
	}

	[Fact]
	public void GenerateSql_WithSelectFromGrant()
	{
		// Act
		var request = new MatchingGrantsRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("FROM authz.grant");
	}

	[Fact]
	public void GenerateSql_WithLikeOperatorsForWildcardMatching()
	{
		// Act
		var request = new MatchingGrantsRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("user_id LIKE");
		sql.ShouldContain("tenant_id LIKE");
		sql.ShouldContain("grant_type LIKE");
		sql.ShouldContain("qualifier LIKE");
	}

	[Fact]
	public void GenerateSql_WithCoalesceForNullUserId()
	{
		// Act
		var request = new MatchingGrantsRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("COALESCE(@UserId, '%')");
	}

	[Fact]
	public void AcceptNullUserId_ForWildcardMatching()
	{
		// Act - null userId matches all users
		var request = new MatchingGrantsRequest(
			null, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Act
		var request = new MatchingGrantsRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}
}
