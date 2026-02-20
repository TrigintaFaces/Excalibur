// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.Grants;

/// <summary>
/// Unit tests for <see cref="ReadGrantRequest"/>.
/// Covers SQL structure, parameter setup, and command timeout.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class ReadGrantRequestShould
{
	private const string UserId = "user-123";
	private const string TenantId = "tenant-1";
	private const string GrantType = "role";
	private const string Qualifier = "admin";

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Act
		var request = new ReadGrantRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(ReadGrantRequest));
	}

	[Fact]
	public void GenerateSql_WithSelectAllFromGrant()
	{
		// Act
		var request = new ReadGrantRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("SELECT *");
		sql.ShouldContain("FROM authz.grant");
	}

	[Fact]
	public void GenerateSql_WithWhereClauseForAllIdentifiers()
	{
		// Act
		var request = new ReadGrantRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("user_id = @UserId");
		sql.ShouldContain("tenant_id = @TenantId");
		sql.ShouldContain("grant_type = @GrantType");
		sql.ShouldContain("qualifier = @Qualifier");
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Act
		var request = new ReadGrantRequest(
			UserId, TenantId, GrantType, Qualifier, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}
}
