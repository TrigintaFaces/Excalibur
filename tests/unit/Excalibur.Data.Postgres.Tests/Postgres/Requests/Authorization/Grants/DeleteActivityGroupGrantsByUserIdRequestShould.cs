// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.Grants;

/// <summary>
/// Unit tests for <see cref="DeleteActivityGroupGrantsByUserIdRequest"/>.
/// Covers SQL structure, parameter setup, and command timeout.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class DeleteActivityGroupGrantsByUserIdRequestShould
{
	private const string UserId = "user-123";
	private const string GrantType = "activity-group";

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Act
		var request = new DeleteActivityGroupGrantsByUserIdRequest(
			UserId, GrantType, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(DeleteActivityGroupGrantsByUserIdRequest));
	}

	[Fact]
	public void GenerateSql_WithDeleteFromGrant()
	{
		// Act
		var request = new DeleteActivityGroupGrantsByUserIdRequest(
			UserId, GrantType, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("DELETE FROM");
		sql.ShouldContain("grant");
	}

	[Fact]
	public void GenerateSql_WithWhereClauseForUserIdAndGrantType()
	{
		// Act
		var request = new DeleteActivityGroupGrantsByUserIdRequest(
			UserId, GrantType, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("user_id = @UserId");
		sql.ShouldContain("grant_type = @GrantType");
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Act
		var request = new DeleteActivityGroupGrantsByUserIdRequest(
			UserId, GrantType, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}
}
