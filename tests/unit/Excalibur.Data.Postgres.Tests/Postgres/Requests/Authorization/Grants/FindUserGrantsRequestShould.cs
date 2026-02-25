// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.Grants;

/// <summary>
/// Unit tests for <see cref="FindUserGrantsRequest"/>.
/// Covers SQL structure, parameter setup, and command timeout.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class FindUserGrantsRequestShould
{
	private const string UserId = "user-123";

	[Fact]
	public void CreateValidRequest_WithUserId()
	{
		// Act
		var request = new FindUserGrantsRequest(UserId, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(FindUserGrantsRequest));
	}

	[Fact]
	public void GenerateSql_SelectingExpectedColumns()
	{
		// Act
		var request = new FindUserGrantsRequest(UserId, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("tenant_id");
		sql.ShouldContain("grant_type");
		sql.ShouldContain("qualifier");
		sql.ShouldContain("expires_on");
	}

	[Fact]
	public void GenerateSql_QueryingGrantTable()
	{
		// Act
		var request = new FindUserGrantsRequest(UserId, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("authz.grant");
	}

	[Fact]
	public void GenerateSql_WithWhereClauseForUserId()
	{
		// Act
		var request = new FindUserGrantsRequest(UserId, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("user_id = @userId");
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Act
		var request = new FindUserGrantsRequest(UserId, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}
}
