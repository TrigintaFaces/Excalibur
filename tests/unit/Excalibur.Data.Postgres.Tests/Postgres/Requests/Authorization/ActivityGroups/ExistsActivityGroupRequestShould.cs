// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.ActivityGroups;

/// <summary>
/// Unit tests for <see cref="ExistsActivityGroupRequest"/>.
/// Covers SQL structure and parameter setup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class ExistsActivityGroupRequestShould
{
	private const string ActivityGroupName = "admin-group";

	[Fact]
	public void CreateValidRequest_WithActivityGroupName()
	{
		// Act
		var request = new ExistsActivityGroupRequest(
			ActivityGroupName, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(ExistsActivityGroupRequest));
	}

	[Fact]
	public void GenerateSql_WithSelectExists()
	{
		// Act
		var request = new ExistsActivityGroupRequest(
			ActivityGroupName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("SELECT EXISTS");
	}

	[Fact]
	public void GenerateSql_WithWhereClauseForName()
	{
		// Act
		var request = new ExistsActivityGroupRequest(
			ActivityGroupName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("name=@activityGroupName");
	}

	[Fact]
	public void GenerateSql_QueryingActivityGroupTable()
	{
		// Act
		var request = new ExistsActivityGroupRequest(
			ActivityGroupName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("authz.activity_group");
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Act
		var request = new ExistsActivityGroupRequest(
			ActivityGroupName, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}
}
