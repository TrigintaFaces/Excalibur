// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.ActivityGroups;

/// <summary>
/// Unit tests for <see cref="CreateActivityGroupRequest"/>.
/// Covers SQL structure, parameter setup, and command timeout.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class CreateActivityGroupRequestShould
{
	private const string TenantId = "tenant-1";
	private const string Name = "admin-group";
	private const string ActivityName = "manage-users";

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Act
		var request = new CreateActivityGroupRequest(
			TenantId, Name, ActivityName, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(CreateActivityGroupRequest));
	}

	[Fact]
	public void GenerateSql_WithInsertIntoActivityGroup()
	{
		// Act
		var request = new CreateActivityGroupRequest(
			TenantId, Name, ActivityName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("INSERT INTO authz.activity_group");
	}

	[Fact]
	public void GenerateSql_WithAllColumnNames()
	{
		// Act
		var request = new CreateActivityGroupRequest(
			TenantId, Name, ActivityName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("tenant_id");
		sql.ShouldContain("name");
		sql.ShouldContain("activity_name");
	}

	[Fact]
	public void GenerateSql_WithParameterPlaceholders()
	{
		// Act
		var request = new CreateActivityGroupRequest(
			TenantId, Name, ActivityName, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("@TenantId");
		sql.ShouldContain("@ActivityGroupName");
		sql.ShouldContain("@ActivityName");
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Act
		var request = new CreateActivityGroupRequest(
			TenantId, Name, ActivityName, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}

	[Fact]
	public void AcceptNullTenantId()
	{
		// Act - tenant ID is optional
		var request = new CreateActivityGroupRequest(
			null, Name, ActivityName, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}
}
