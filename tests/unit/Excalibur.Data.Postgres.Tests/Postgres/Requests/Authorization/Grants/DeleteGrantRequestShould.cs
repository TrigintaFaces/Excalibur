// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.Grants;

/// <summary>
/// Unit tests for <see cref="DeleteGrantRequest"/>.
/// Covers SQL structure, parameter setup, and archiving behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class DeleteGrantRequestShould
{
	private const string UserId = "user-123";
	private const string TenantId = "tenant-1";
	private const string GrantType = "role";
	private const string Qualifier = "admin";
	private const string RevokedBy = "admin-user";
	private static readonly DateTimeOffset RevokedOn = DateTimeOffset.UtcNow;

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Act
		var request = new DeleteGrantRequest(
			UserId, TenantId, GrantType, Qualifier, RevokedBy, RevokedOn,
			CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(DeleteGrantRequest));
	}

	[Fact]
	public void GenerateSql_WithInsertIntoGrantHistory()
	{
		// Act
		var request = new DeleteGrantRequest(
			UserId, TenantId, GrantType, Qualifier, RevokedBy, RevokedOn,
			CancellationToken.None);

		// Assert - Archives the grant before deleting
		var sql = request.Command.CommandText;
		sql.ShouldContain("INSERT INTO authz.grant_history");
	}

	[Fact]
	public void GenerateSql_WithDeleteFromGrant()
	{
		// Act
		var request = new DeleteGrantRequest(
			UserId, TenantId, GrantType, Qualifier, RevokedBy, RevokedOn,
			CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("DELETE FROM authz.grant");
	}

	[Fact]
	public void GenerateSql_WithWhereClauseForAllIdentifiers()
	{
		// Act
		var request = new DeleteGrantRequest(
			UserId, TenantId, GrantType, Qualifier, RevokedBy, RevokedOn,
			CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("user_id = @UserId");
		sql.ShouldContain("tenant_id = @TenantId");
		sql.ShouldContain("grant_type = @GrantType");
		sql.ShouldContain("qualifier = @Qualifier");
	}

	[Fact]
	public void GenerateSql_WithRevokedByAndRevokedOnParameters()
	{
		// Act
		var request = new DeleteGrantRequest(
			UserId, TenantId, GrantType, Qualifier, RevokedBy, RevokedOn,
			CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("@RevokedBy");
		sql.ShouldContain("@RevokedOn");
	}

	[Fact]
	public void AcceptNullRevokedBy()
	{
		// Act - revokedBy is optional
		var request = new DeleteGrantRequest(
			UserId, TenantId, GrantType, Qualifier, revokedBy: null, RevokedOn,
			CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void AcceptNullRevokedOn()
	{
		// Act - revokedOn is optional
		var request = new DeleteGrantRequest(
			UserId, TenantId, GrantType, Qualifier, RevokedBy, revokedOn: null,
			CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Act
		var request = new DeleteGrantRequest(
			UserId, TenantId, GrantType, Qualifier, RevokedBy, RevokedOn,
			CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}
}
