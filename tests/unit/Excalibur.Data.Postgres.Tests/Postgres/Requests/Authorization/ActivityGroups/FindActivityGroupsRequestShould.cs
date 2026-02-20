// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.ActivityGroups;

/// <summary>
/// Unit tests for <see cref="FindActivityGroupsRequest"/>.
/// Covers SQL structure and command configuration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class FindActivityGroupsRequestShould
{
	[Fact]
	public void CreateValidRequest()
	{
		// Act
		var request = new FindActivityGroupsRequest(CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(FindActivityGroupsRequest));
	}

	[Fact]
	public void GenerateSql_WithSelectFromActivityGroup()
	{
		// Act
		var request = new FindActivityGroupsRequest(CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("FROM authz.activity_group");
	}

	[Fact]
	public void GenerateSql_SelectingExpectedColumns()
	{
		// Act
		var request = new FindActivityGroupsRequest(CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("name");
		sql.ShouldContain("tenant_id");
		sql.ShouldContain("activity_name");
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Act
		var request = new FindActivityGroupsRequest(CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}
}
