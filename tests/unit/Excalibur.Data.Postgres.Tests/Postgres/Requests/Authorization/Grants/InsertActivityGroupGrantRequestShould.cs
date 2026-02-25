// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.Grants;

/// <summary>
/// Unit tests for <see cref="InsertActivityGroupGrantRequest"/>.
/// Covers SQL structure, parameter setup, and optional parameters.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class InsertActivityGroupGrantRequestShould
{
	private const string UserId = "user-123";
	private const string FullName = "John Doe";
	private const string TenantId = "tenant-1";
	private const string GrantType = "activity-group";
	private const string Qualifier = "admin-group";
	private const string GrantedBy = "system";
	private static readonly DateTimeOffset ExpiresOn = DateTimeOffset.UtcNow.AddDays(30);

	[Fact]
	public void CreateValidRequest_WithAllParameters()
	{
		// Act
		var request = new InsertActivityGroupGrantRequest(
			UserId, FullName, TenantId, GrantType, Qualifier,
			ExpiresOn, GrantedBy, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(InsertActivityGroupGrantRequest));
	}

	[Fact]
	public void GenerateSql_WithInsertIntoGrant()
	{
		// Act
		var request = new InsertActivityGroupGrantRequest(
			UserId, FullName, TenantId, GrantType, Qualifier,
			ExpiresOn, GrantedBy, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("INSERT INTO");
		sql.ShouldContain("Grant");
	}

	[Fact]
	public void GenerateSql_WithAllGrantColumnNames()
	{
		// Act
		var request = new InsertActivityGroupGrantRequest(
			UserId, FullName, TenantId, GrantType, Qualifier,
			ExpiresOn, GrantedBy, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("UserId");
		sql.ShouldContain("FullName");
		sql.ShouldContain("TenantId");
		sql.ShouldContain("GrantType");
		sql.ShouldContain("Qualifier");
		sql.ShouldContain("ExpiresOn");
		sql.ShouldContain("GrantedBy");
		sql.ShouldContain("GrantedOn");
	}

	[Fact]
	public void GenerateSql_WithNowUtcForGrantedOn()
	{
		// Act
		var request = new InsertActivityGroupGrantRequest(
			UserId, FullName, TenantId, GrantType, Qualifier,
			ExpiresOn, GrantedBy, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("NOW() AT TIME ZONE 'UTC'");
	}

	[Fact]
	public void AcceptNullTenantId()
	{
		// Act - tenantId is optional
		var request = new InsertActivityGroupGrantRequest(
			UserId, FullName, null, GrantType, Qualifier,
			ExpiresOn, GrantedBy, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void AcceptNullExpiresOn()
	{
		// Act - expiresOn is optional
		var request = new InsertActivityGroupGrantRequest(
			UserId, FullName, TenantId, GrantType, Qualifier,
			null, GrantedBy, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Act
		var request = new InsertActivityGroupGrantRequest(
			UserId, FullName, TenantId, GrantType, Qualifier,
			ExpiresOn, GrantedBy, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}
}
