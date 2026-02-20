// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.Grants;

/// <summary>
/// Unit tests for <see cref="DeleteAllActivityGroupGrantsRequest"/>.
/// Covers SQL structure, parameter setup, and command timeout.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class DeleteAllActivityGroupGrantsRequestShould
{
	private const string GrantType = "activity-group";

	[Fact]
	public void CreateValidRequest_WithGrantType()
	{
		// Act
		var request = new DeleteAllActivityGroupGrantsRequest(
			GrantType, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(DeleteAllActivityGroupGrantsRequest));
	}

	[Fact]
	public void GenerateSql_WithDeleteFromGrant()
	{
		// Act
		var request = new DeleteAllActivityGroupGrantsRequest(
			GrantType, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("DELETE FROM");
		sql.ShouldContain("grant");
	}

	[Fact]
	public void GenerateSql_WithWhereClauseForGrantType()
	{
		// Act
		var request = new DeleteAllActivityGroupGrantsRequest(
			GrantType, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("grant_type = @GrantType");
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Act
		var request = new DeleteAllActivityGroupGrantsRequest(
			GrantType, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}
}
