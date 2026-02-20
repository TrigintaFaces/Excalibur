// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.ActivityGroups;

/// <summary>
/// Unit tests for <see cref="DeleteAllActivityGroupsRequest"/>.
/// Covers SQL structure and command timeout.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class DeleteAllActivityGroupsRequestShould
{
	[Fact]
	public void CreateValidRequest()
	{
		// Act
		var request = new DeleteAllActivityGroupsRequest(CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(DeleteAllActivityGroupsRequest));
	}

	[Fact]
	public void GenerateSql_WithDeleteFromActivityGroup()
	{
		// Act
		var request = new DeleteAllActivityGroupsRequest(CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("DELETE FROM authz.activity_group");
	}

	[Fact]
	public void GenerateSql_WithNoWhereClause()
	{
		// Act - Deletes ALL records
		var request = new DeleteAllActivityGroupsRequest(CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldNotContain("WHERE");
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Act
		var request = new DeleteAllActivityGroupsRequest(CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}
}
