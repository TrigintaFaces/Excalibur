// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.SqlServer.RequestProviders;

namespace Excalibur.Tests.Data.SqlServer.Requests;

/// <summary>
/// Unit tests for SQL Server authorization Request classes in Excalibur.Data.SqlServer.
/// Covers all ActivityGroup and Grant request types: constructor validation,
/// SQL structure, parameter setup, table names, and DataRequestBase properties.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "SqlServer")]
public sealed class SqlServerAuthorizationRequestsShould
{
	private static readonly CancellationToken Ct = CancellationToken.None;

	#region CreateActivityGroupRequest

	[Fact]
	public void CreateActivityGroupRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new CreateActivityGroupRequest("tenant-1", "AdminGroup", "ManageUsers", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("INSERT INTO authz.ActivityGroup");
	}

	[Fact]
	public void CreateActivityGroupRequest_ContainCorrectColumns()
	{
		var request = new CreateActivityGroupRequest("tenant-1", "AdminGroup", "ManageUsers", Ct);

		request.Command.CommandText.ShouldContain("TenantId");
		request.Command.CommandText.ShouldContain("Name");
		request.Command.CommandText.ShouldContain("ActivityName");
	}

	[Fact]
	public void CreateActivityGroupRequest_HaveParametersConfigured()
	{
		var request = new CreateActivityGroupRequest("tenant-1", "AdminGroup", "ManageUsers", Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@TenantId");
		request.Command.CommandText.ShouldContain("@ActivityGroupName");
		request.Command.CommandText.ShouldContain("@ActivityName");
	}

	[Fact]
	public void CreateActivityGroupRequest_AcceptNullTenantId()
	{
		var request = new CreateActivityGroupRequest(null, "AdminGroup", "ManageUsers", Ct);

		request.ShouldNotBeNull();
	}

	[Fact]
	public void CreateActivityGroupRequest_HaveCorrectRequestType()
	{
		var request = new CreateActivityGroupRequest("tenant-1", "AdminGroup", "ManageUsers", Ct);

		request.RequestType.ShouldBe("CreateActivityGroupRequest");
	}

	[Fact]
	public void CreateActivityGroupRequest_HaveResolveAsync()
	{
		var request = new CreateActivityGroupRequest("tenant-1", "AdminGroup", "ManageUsers", Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void CreateActivityGroupRequest_HaveUniqueRequestId()
	{
		var request1 = new CreateActivityGroupRequest("tenant-1", "AdminGroup", "ManageUsers", Ct);
		var request2 = new CreateActivityGroupRequest("tenant-1", "AdminGroup", "ManageUsers", Ct);

		request1.RequestId.ShouldNotBe(request2.RequestId);
	}

	#endregion

	#region DeleteAllActivityGroupsRequest

	[Fact]
	public void DeleteAllActivityGroupsRequest_CreateSuccessfully()
	{
		var request = new DeleteAllActivityGroupsRequest(Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("DELETE FROM authz.ActivityGroup");
	}

	[Fact]
	public void DeleteAllActivityGroupsRequest_HaveCorrectRequestType()
	{
		var request = new DeleteAllActivityGroupsRequest(Ct);

		request.RequestType.ShouldBe("DeleteAllActivityGroupsRequest");
	}

	[Fact]
	public void DeleteAllActivityGroupsRequest_HaveResolveAsync()
	{
		var request = new DeleteAllActivityGroupsRequest(Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void DeleteAllActivityGroupsRequest_HaveRequestId()
	{
		var request = new DeleteAllActivityGroupsRequest(Ct);

		request.RequestId.ShouldNotBeNullOrWhiteSpace();
		Guid.TryParse(request.RequestId, out _).ShouldBeTrue();
	}

	[Fact]
	public void DeleteAllActivityGroupsRequest_HaveCreatedAtTimestamp()
	{
		var before = DateTimeOffset.UtcNow;

		var request = new DeleteAllActivityGroupsRequest(Ct);

		request.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		request.CreatedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	#endregion

	#region FindActivityGroupsRequest

	[Fact]
	public void FindActivityGroupsRequest_CreateSuccessfully()
	{
		var request = new FindActivityGroupsRequest(Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("SELECT");
		request.Command.CommandText.ShouldContain("FROM authz.ActivityGroup");
	}

	[Fact]
	public void FindActivityGroupsRequest_SelectCorrectColumns()
	{
		var request = new FindActivityGroupsRequest(Ct);

		request.Command.CommandText.ShouldContain("Name");
		request.Command.CommandText.ShouldContain("TenantId");
		request.Command.CommandText.ShouldContain("ActivityName");
	}

	[Fact]
	public void FindActivityGroupsRequest_HaveCorrectRequestType()
	{
		var request = new FindActivityGroupsRequest(Ct);

		request.RequestType.ShouldBe("FindActivityGroupsRequest");
	}

	[Fact]
	public void FindActivityGroupsRequest_HaveResolveAsync()
	{
		var request = new FindActivityGroupsRequest(Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region ExistsActivityGroupRequest

	[Fact]
	public void ExistsActivityGroupRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new ExistsActivityGroupRequest("AdminGroup", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("SELECT EXISTS");
		request.Command.CommandText.ShouldContain("FROM authz.ActivityGroup");
	}

	[Fact]
	public void ExistsActivityGroupRequest_FilterByName()
	{
		var request = new ExistsActivityGroupRequest("AdminGroup", Ct);

		request.Command.CommandText.ShouldContain("Name=@activityGroupName");
	}

	[Fact]
	public void ExistsActivityGroupRequest_HaveParametersConfigured()
	{
		var request = new ExistsActivityGroupRequest("AdminGroup", Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@activityGroupName");
	}

	[Fact]
	public void ExistsActivityGroupRequest_HaveCorrectRequestType()
	{
		var request = new ExistsActivityGroupRequest("AdminGroup", Ct);

		request.RequestType.ShouldBe("ExistsActivityGroupRequest");
	}

	[Fact]
	public void ExistsActivityGroupRequest_HaveResolveAsync()
	{
		var request = new ExistsActivityGroupRequest("AdminGroup", Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region SaveGrantRequest

	[Fact]
	public void SaveGrantRequest_CreateSuccessfully_WithValidParameters()
	{
		var grant = CreateGrant();

		var request = new SaveGrantRequest(grant, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("INSERT INTO Authz.Grant");
	}

	[Fact]
	public void SaveGrantRequest_ContainCorrectColumns()
	{
		var grant = CreateGrant();

		var request = new SaveGrantRequest(grant, Ct);

		request.Command.CommandText.ShouldContain("UserId");
		request.Command.CommandText.ShouldContain("FullName");
		request.Command.CommandText.ShouldContain("TenantId");
		request.Command.CommandText.ShouldContain("GrantType");
		request.Command.CommandText.ShouldContain("Qualifier");
		request.Command.CommandText.ShouldContain("ExpiresOn");
		request.Command.CommandText.ShouldContain("GrantedBy");
		request.Command.CommandText.ShouldContain("GrantedOn");
	}

	[Fact]
	public void SaveGrantRequest_HaveParametersConfigured()
	{
		var grant = CreateGrant();

		var request = new SaveGrantRequest(grant, Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@UserId");
		request.Command.CommandText.ShouldContain("@FullName");
		request.Command.CommandText.ShouldContain("@TenantId");
		request.Command.CommandText.ShouldContain("@GrantType");
		request.Command.CommandText.ShouldContain("@Qualifier");
		request.Command.CommandText.ShouldContain("@ExpiresOn");
		request.Command.CommandText.ShouldContain("@GrantedBy");
		request.Command.CommandText.ShouldContain("@GrantedOn");
	}

	[Fact]
	public void SaveGrantRequest_ThrowOnNullGrant()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SaveGrantRequest(null!, Ct));
	}

	[Fact]
	public void SaveGrantRequest_HaveCorrectRequestType()
	{
		var grant = CreateGrant();

		var request = new SaveGrantRequest(grant, Ct);

		request.RequestType.ShouldBe("SaveGrantRequest");
	}

	[Fact]
	public void SaveGrantRequest_HaveResolveAsync()
	{
		var grant = CreateGrant();

		var request = new SaveGrantRequest(grant, Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region ReadGrantRequest

	[Fact]
	public void ReadGrantRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new ReadGrantRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("SELECT");
		request.Command.CommandText.ShouldContain("FROM Authz.Grant");
	}

	[Fact]
	public void ReadGrantRequest_FilterByAllKeys()
	{
		var request = new ReadGrantRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.Command.CommandText.ShouldContain("UserId = @UserId");
		request.Command.CommandText.ShouldContain("TenantId = @TenantId");
		request.Command.CommandText.ShouldContain("GrantType = @GrantType");
		request.Command.CommandText.ShouldContain("Qualifier = @Qualifier");
	}

	[Fact]
	public void ReadGrantRequest_HaveParametersConfigured()
	{
		var request = new ReadGrantRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@UserId");
		request.Command.CommandText.ShouldContain("@TenantId");
		request.Command.CommandText.ShouldContain("@GrantType");
		request.Command.CommandText.ShouldContain("@Qualifier");
	}

	[Fact]
	public void ReadGrantRequest_HaveCorrectRequestType()
	{
		var request = new ReadGrantRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.RequestType.ShouldBe("ReadGrantRequest");
	}

	[Fact]
	public void ReadGrantRequest_HaveResolveAsync()
	{
		var request = new ReadGrantRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region ReadAllGrantsRequest

	[Fact]
	public void ReadAllGrantsRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new ReadAllGrantsRequest("user-1", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("SELECT");
		request.Command.CommandText.ShouldContain("FROM Authz.Grant");
	}

	[Fact]
	public void ReadAllGrantsRequest_FilterByUserId()
	{
		var request = new ReadAllGrantsRequest("user-1", Ct);

		request.Command.CommandText.ShouldContain("UserId = @UserId");
	}

	[Fact]
	public void ReadAllGrantsRequest_HaveParametersConfigured()
	{
		var request = new ReadAllGrantsRequest("user-1", Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@UserId");
	}

	[Fact]
	public void ReadAllGrantsRequest_HaveCorrectRequestType()
	{
		var request = new ReadAllGrantsRequest("user-1", Ct);

		request.RequestType.ShouldBe("ReadAllGrantsRequest");
	}

	[Fact]
	public void ReadAllGrantsRequest_HaveResolveAsync()
	{
		var request = new ReadAllGrantsRequest("user-1", Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region DeleteGrantRequest

	[Fact]
	public void DeleteGrantRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new DeleteGrantRequest("user-1", "tenant-1", "Role", "Admin", "admin-user", DateTimeOffset.UtcNow, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("DELETE FROM Authz.Grant");
	}

	[Fact]
	public void DeleteGrantRequest_ArchiveToGrantHistory()
	{
		var request = new DeleteGrantRequest("user-1", "tenant-1", "Role", "Admin", "admin-user", DateTimeOffset.UtcNow, Ct);

		request.Command.CommandText.ShouldContain("INSERT INTO Authz.GrantHistory");
	}

	[Fact]
	public void DeleteGrantRequest_FilterByAllKeys()
	{
		var request = new DeleteGrantRequest("user-1", "tenant-1", "Role", "Admin", "admin-user", DateTimeOffset.UtcNow, Ct);

		request.Command.CommandText.ShouldContain("UserId = @UserId");
		request.Command.CommandText.ShouldContain("TenantId = @TenantId");
		request.Command.CommandText.ShouldContain("GrantType = @GrantType");
		request.Command.CommandText.ShouldContain("Qualifier = @Qualifier");
	}

	[Fact]
	public void DeleteGrantRequest_HaveParametersConfigured()
	{
		var request = new DeleteGrantRequest("user-1", "tenant-1", "Role", "Admin", "admin-user", DateTimeOffset.UtcNow, Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@UserId");
		request.Command.CommandText.ShouldContain("@TenantId");
		request.Command.CommandText.ShouldContain("@GrantType");
		request.Command.CommandText.ShouldContain("@Qualifier");
		request.Command.CommandText.ShouldContain("@RevokedBy");
		request.Command.CommandText.ShouldContain("@RevokedOn");
	}

	[Fact]
	public void DeleteGrantRequest_AcceptNullRevokedBy()
	{
		var request = new DeleteGrantRequest("user-1", "tenant-1", "Role", "Admin", null, null, Ct);

		request.ShouldNotBeNull();
	}

	[Fact]
	public void DeleteGrantRequest_AcceptNullRevokedOn()
	{
		var request = new DeleteGrantRequest("user-1", "tenant-1", "Role", "Admin", "admin-user", null, Ct);

		request.ShouldNotBeNull();
	}

	[Fact]
	public void DeleteGrantRequest_HaveCorrectRequestType()
	{
		var request = new DeleteGrantRequest("user-1", "tenant-1", "Role", "Admin", "admin-user", DateTimeOffset.UtcNow, Ct);

		request.RequestType.ShouldBe("DeleteGrantRequest");
	}

	[Fact]
	public void DeleteGrantRequest_HaveResolveAsync()
	{
		var request = new DeleteGrantRequest("user-1", "tenant-1", "Role", "Admin", "admin-user", DateTimeOffset.UtcNow, Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void DeleteGrantRequest_ContainRevokedByParameter()
	{
		var request = new DeleteGrantRequest("user-1", "tenant-1", "Role", "Admin", "admin-user", DateTimeOffset.UtcNow, Ct);

		request.Command.CommandText.ShouldContain("@RevokedBy");
		request.Command.CommandText.ShouldContain("@RevokedOn");
	}

	#endregion

	#region DeleteAllActivityGroupGrantsRequest

	[Fact]
	public void DeleteAllActivityGroupGrantsRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new DeleteAllActivityGroupGrantsRequest("ActivityGroupGrant", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("DELETE FROM Authz.Grant");
	}

	[Fact]
	public void DeleteAllActivityGroupGrantsRequest_FilterByGrantType()
	{
		var request = new DeleteAllActivityGroupGrantsRequest("ActivityGroupGrant", Ct);

		request.Command.CommandText.ShouldContain("GrantType = @GrantType");
	}

	[Fact]
	public void DeleteAllActivityGroupGrantsRequest_HaveParametersConfigured()
	{
		var request = new DeleteAllActivityGroupGrantsRequest("ActivityGroupGrant", Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@GrantType");
	}

	[Fact]
	public void DeleteAllActivityGroupGrantsRequest_HaveCorrectRequestType()
	{
		var request = new DeleteAllActivityGroupGrantsRequest("ActivityGroupGrant", Ct);

		request.RequestType.ShouldBe("DeleteAllActivityGroupGrantsRequest");
	}

	[Fact]
	public void DeleteAllActivityGroupGrantsRequest_HaveResolveAsync()
	{
		var request = new DeleteAllActivityGroupGrantsRequest("ActivityGroupGrant", Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region DeleteActivityGroupGrantsByUserIdRequest

	[Fact]
	public void DeleteActivityGroupGrantsByUserIdRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new DeleteActivityGroupGrantsByUserIdRequest("user-1", "ActivityGroupGrant", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("DELETE FROM Authz.Grant");
	}

	[Fact]
	public void DeleteActivityGroupGrantsByUserIdRequest_FilterByUserIdAndGrantType()
	{
		var request = new DeleteActivityGroupGrantsByUserIdRequest("user-1", "ActivityGroupGrant", Ct);

		request.Command.CommandText.ShouldContain("UserId = @UserId");
		request.Command.CommandText.ShouldContain("GrantType = @GrantType");
	}

	[Fact]
	public void DeleteActivityGroupGrantsByUserIdRequest_HaveParametersConfigured()
	{
		var request = new DeleteActivityGroupGrantsByUserIdRequest("user-1", "ActivityGroupGrant", Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@UserId");
		request.Command.CommandText.ShouldContain("@GrantType");
	}

	[Fact]
	public void DeleteActivityGroupGrantsByUserIdRequest_HaveCorrectRequestType()
	{
		var request = new DeleteActivityGroupGrantsByUserIdRequest("user-1", "ActivityGroupGrant", Ct);

		request.RequestType.ShouldBe("DeleteActivityGroupGrantsByUserIdRequest");
	}

	[Fact]
	public void DeleteActivityGroupGrantsByUserIdRequest_HaveResolveAsync()
	{
		var request = new DeleteActivityGroupGrantsByUserIdRequest("user-1", "ActivityGroupGrant", Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region ExistsGrantRequest

	[Fact]
	public void ExistsGrantRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new ExistsGrantRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("FROM Authz.Grant");
	}

	[Fact]
	public void ExistsGrantRequest_CheckExistenceWithExpiryFilter()
	{
		var request = new ExistsGrantRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.Command.CommandText.ShouldContain("WHEN EXISTS");
		request.Command.CommandText.ShouldContain("ISNULL(ExpiresOn, '9999-12-31') > GETUTCDATE()");
	}

	[Fact]
	public void ExistsGrantRequest_FilterByAllKeys()
	{
		var request = new ExistsGrantRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.Command.CommandText.ShouldContain("UserId = @UserId");
		request.Command.CommandText.ShouldContain("TenantId = @TenantId");
		request.Command.CommandText.ShouldContain("GrantType = @GrantType");
		request.Command.CommandText.ShouldContain("Qualifier = @Qualifier");
	}

	[Fact]
	public void ExistsGrantRequest_HaveParametersConfigured()
	{
		var request = new ExistsGrantRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@UserId");
		request.Command.CommandText.ShouldContain("@TenantId");
		request.Command.CommandText.ShouldContain("@GrantType");
		request.Command.CommandText.ShouldContain("@Qualifier");
	}

	[Fact]
	public void ExistsGrantRequest_HaveCorrectRequestType()
	{
		var request = new ExistsGrantRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.RequestType.ShouldBe("ExistsGrantRequest");
	}

	[Fact]
	public void ExistsGrantRequest_HaveResolveAsync()
	{
		var request = new ExistsGrantRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region FindUserGrantsRequest

	[Fact]
	public void FindUserGrantsRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new FindUserGrantsRequest("user-1", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("SELECT");
		request.Command.CommandText.ShouldContain("FROM Authz.Grant");
	}

	[Fact]
	public void FindUserGrantsRequest_SelectCorrectColumns()
	{
		var request = new FindUserGrantsRequest("user-1", Ct);

		request.Command.CommandText.ShouldContain("TenantId");
		request.Command.CommandText.ShouldContain("GrantType");
		request.Command.CommandText.ShouldContain("Qualifier");
		request.Command.CommandText.ShouldContain("ExpiresOn");
	}

	[Fact]
	public void FindUserGrantsRequest_FilterByUserId()
	{
		var request = new FindUserGrantsRequest("user-1", Ct);

		request.Command.CommandText.ShouldContain("UserId = @UserId");
	}

	[Fact]
	public void FindUserGrantsRequest_HaveParametersConfigured()
	{
		var request = new FindUserGrantsRequest("user-1", Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@UserId");
	}

	[Fact]
	public void FindUserGrantsRequest_HaveCorrectRequestType()
	{
		var request = new FindUserGrantsRequest("user-1", Ct);

		request.RequestType.ShouldBe("FindUserGrantsRequest");
	}

	[Fact]
	public void FindUserGrantsRequest_HaveResolveAsync()
	{
		var request = new FindUserGrantsRequest("user-1", Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region GetDistinctActivityGroupGrantUserIdsRequest

	[Fact]
	public void GetDistinctActivityGroupGrantUserIdsRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new GetDistinctActivityGroupGrantUserIdsRequest("ActivityGroupGrant", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("SELECT DISTINCT UserId");
		request.Command.CommandText.ShouldContain("FROM Authz.Grant");
	}

	[Fact]
	public void GetDistinctActivityGroupGrantUserIdsRequest_FilterByGrantType()
	{
		var request = new GetDistinctActivityGroupGrantUserIdsRequest("ActivityGroupGrant", Ct);

		request.Command.CommandText.ShouldContain("GrantType = @GrantType");
	}

	[Fact]
	public void GetDistinctActivityGroupGrantUserIdsRequest_HaveParametersConfigured()
	{
		var request = new GetDistinctActivityGroupGrantUserIdsRequest("ActivityGroupGrant", Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@GrantType");
	}

	[Fact]
	public void GetDistinctActivityGroupGrantUserIdsRequest_HaveCorrectRequestType()
	{
		var request = new GetDistinctActivityGroupGrantUserIdsRequest("ActivityGroupGrant", Ct);

		request.RequestType.ShouldBe("GetDistinctActivityGroupGrantUserIdsRequest");
	}

	[Fact]
	public void GetDistinctActivityGroupGrantUserIdsRequest_HaveResolveAsync()
	{
		var request = new GetDistinctActivityGroupGrantUserIdsRequest("ActivityGroupGrant", Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region InsertActivityGroupGrantRequest

	[Fact]
	public void InsertActivityGroupGrantRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new InsertActivityGroupGrantRequest(
			"user-1", "John Doe", "tenant-1", "ActivityGroupGrant",
			"AdminGroup", DateTimeOffset.UtcNow.AddDays(30), "admin-user", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("INSERT INTO Authz.Grant");
	}

	[Fact]
	public void InsertActivityGroupGrantRequest_ContainCorrectColumns()
	{
		var request = new InsertActivityGroupGrantRequest(
			"user-1", "John Doe", "tenant-1", "ActivityGroupGrant",
			"AdminGroup", null, "admin-user", Ct);

		request.Command.CommandText.ShouldContain("UserId");
		request.Command.CommandText.ShouldContain("FullName");
		request.Command.CommandText.ShouldContain("TenantId");
		request.Command.CommandText.ShouldContain("GrantType");
		request.Command.CommandText.ShouldContain("Qualifier");
		request.Command.CommandText.ShouldContain("ExpiresOn");
		request.Command.CommandText.ShouldContain("GrantedBy");
		request.Command.CommandText.ShouldContain("GrantedOn");
	}

	[Fact]
	public void InsertActivityGroupGrantRequest_UseGetUtcDateForGrantedOn()
	{
		var request = new InsertActivityGroupGrantRequest(
			"user-1", "John Doe", "tenant-1", "ActivityGroupGrant",
			"AdminGroup", null, "admin-user", Ct);

		request.Command.CommandText.ShouldContain("GETUTCDATE()");
	}

	[Fact]
	public void InsertActivityGroupGrantRequest_HaveParametersConfigured()
	{
		var request = new InsertActivityGroupGrantRequest(
			"user-1", "John Doe", "tenant-1", "ActivityGroupGrant",
			"AdminGroup", null, "admin-user", Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@UserId");
		request.Command.CommandText.ShouldContain("@FullName");
		request.Command.CommandText.ShouldContain("@TenantId");
		request.Command.CommandText.ShouldContain("@GrantType");
		request.Command.CommandText.ShouldContain("@Qualifier");
		request.Command.CommandText.ShouldContain("@ExpiresOn");
		request.Command.CommandText.ShouldContain("@GrantedBy");
	}

	[Fact]
	public void InsertActivityGroupGrantRequest_AcceptNullTenantId()
	{
		var request = new InsertActivityGroupGrantRequest(
			"user-1", "John Doe", null, "ActivityGroupGrant",
			"AdminGroup", null, "admin-user", Ct);

		request.ShouldNotBeNull();
	}

	[Fact]
	public void InsertActivityGroupGrantRequest_AcceptNullExpiresOn()
	{
		var request = new InsertActivityGroupGrantRequest(
			"user-1", "John Doe", "tenant-1", "ActivityGroupGrant",
			"AdminGroup", null, "admin-user", Ct);

		request.ShouldNotBeNull();
	}

	[Fact]
	public void InsertActivityGroupGrantRequest_HaveCorrectRequestType()
	{
		var request = new InsertActivityGroupGrantRequest(
			"user-1", "John Doe", "tenant-1", "ActivityGroupGrant",
			"AdminGroup", null, "admin-user", Ct);

		request.RequestType.ShouldBe("InsertActivityGroupGrantRequest");
	}

	[Fact]
	public void InsertActivityGroupGrantRequest_HaveResolveAsync()
	{
		var request = new InsertActivityGroupGrantRequest(
			"user-1", "John Doe", "tenant-1", "ActivityGroupGrant",
			"AdminGroup", null, "admin-user", Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region MatchingGrantsRequest

	[Fact]
	public void MatchingGrantsRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new MatchingGrantsRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("SELECT");
		request.Command.CommandText.ShouldContain("FROM Authz.Grant");
	}

	[Fact]
	public void MatchingGrantsRequest_UseLikeForFlexibleMatching()
	{
		var request = new MatchingGrantsRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.Command.CommandText.ShouldContain("UserId LIKE");
		request.Command.CommandText.ShouldContain("TenantId LIKE");
		request.Command.CommandText.ShouldContain("GrantType LIKE");
		request.Command.CommandText.ShouldContain("Qualifier LIKE");
	}

	[Fact]
	public void MatchingGrantsRequest_HaveParametersConfigured()
	{
		var request = new MatchingGrantsRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.Parameters.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("@UserId");
		request.Command.CommandText.ShouldContain("@TenantId");
		request.Command.CommandText.ShouldContain("@GrantType");
		request.Command.CommandText.ShouldContain("@Qualifier");
	}

	[Fact]
	public void MatchingGrantsRequest_AcceptNullUserId()
	{
		var request = new MatchingGrantsRequest(null, "tenant-1", "Role", "Admin", Ct);

		request.ShouldNotBeNull();
	}

	[Fact]
	public void MatchingGrantsRequest_HandleNullUserIdWithWildcard()
	{
		var request = new MatchingGrantsRequest(null, "tenant-1", "Role", "Admin", Ct);

		// When userId is null, ISNULL(@UserId, '%') should match all
		request.Command.CommandText.ShouldContain("ISNULL(@UserId, '%')");
	}

	[Fact]
	public void MatchingGrantsRequest_HaveCorrectRequestType()
	{
		var request = new MatchingGrantsRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.RequestType.ShouldBe("MatchingGrantsRequest");
	}

	[Fact]
	public void MatchingGrantsRequest_HaveResolveAsync()
	{
		var request = new MatchingGrantsRequest("user-1", "tenant-1", "Role", "Admin", Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region DataRequestBase Properties (Cross-cutting)

	[Fact]
	public void AllAuthorizationRequests_HaveUniqueRequestIds()
	{
		var request1 = new ReadAllGrantsRequest("user-1", Ct);
		var request2 = new ReadAllGrantsRequest("user-1", Ct);

		request1.RequestId.ShouldNotBeNullOrEmpty();
		request2.RequestId.ShouldNotBeNullOrEmpty();
		request1.RequestId.ShouldNotBe(request2.RequestId);
	}

	[Fact]
	public void AllAuthorizationRequests_HaveValidGuidRequestId()
	{
		var request = new ReadAllGrantsRequest("user-1", Ct);

		Guid.TryParse(request.RequestId, out _).ShouldBeTrue();
	}

	[Fact]
	public void AllAuthorizationRequests_HaveCreatedAtTimestamp()
	{
		var before = DateTimeOffset.UtcNow;

		var request = new ReadAllGrantsRequest("user-1", Ct);

		request.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		request.CreatedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public void AllAuthorizationRequests_HaveNullCorrelationIdByDefault()
	{
		var request = new ReadAllGrantsRequest("user-1", Ct);

		request.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void AllAuthorizationRequests_AllowSettingCorrelationId()
	{
		var request = new ReadAllGrantsRequest("user-1", Ct);
		var correlationId = Guid.NewGuid().ToString();

		request.CorrelationId = correlationId;

		request.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void AllAuthorizationRequests_HaveNullMetadataByDefault()
	{
		var request = new ReadAllGrantsRequest("user-1", Ct);

		request.Metadata.ShouldBeNull();
	}

	[Fact]
	public void AllAuthorizationRequests_AllowSettingMetadata()
	{
		var request = new ReadAllGrantsRequest("user-1", Ct);

		request.Metadata = new Dictionary<string, object> { ["key"] = "value" };

		request.Metadata.ShouldNotBeNull();
		request.Metadata["key"].ShouldBe("value");
	}

	#endregion

	#region Helpers

	private static Grant CreateGrant() =>
		new(
			"user-1",
			"John Doe",
			"tenant-1",
			"Role",
			"Admin",
			DateTimeOffset.UtcNow.AddDays(30),
			"admin-user",
			DateTimeOffset.UtcNow);

	#endregion
}
