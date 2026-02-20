// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Tests.Postgres.Requests.Authorization.Grants;

/// <summary>
/// Unit tests for <see cref="SaveGrantRequest"/>.
/// Covers constructor validation, SQL structure, and parameter setup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class SaveGrantRequestShould
{
	private static Grant CreateTestGrant() => new(
		UserId: "user-123",
		FullName: "John Doe",
		TenantId: "tenant-1",
		GrantType: "role",
		Qualifier: "admin",
		ExpiresOn: DateTimeOffset.UtcNow.AddDays(30),
		GrantedBy: "system",
		GrantedOn: DateTimeOffset.UtcNow);

	[Fact]
	public void CreateValidRequest_WithGrant()
	{
		// Arrange
		var grant = CreateTestGrant();

		// Act
		var request = new SaveGrantRequest(grant, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
		request.RequestType.ShouldBe(nameof(SaveGrantRequest));
	}

	[Fact]
	public void GenerateSql_WithInsertIntoGrant()
	{
		// Arrange
		var grant = CreateTestGrant();

		// Act
		var request = new SaveGrantRequest(grant, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("INSERT INTO");
		sql.ShouldContain("grant");
	}

	[Fact]
	public void GenerateSql_WithAllGrantColumnNames()
	{
		// Arrange
		var grant = CreateTestGrant();

		// Act
		var request = new SaveGrantRequest(grant, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("user_id");
		sql.ShouldContain("full_name");
		sql.ShouldContain("tenant_id");
		sql.ShouldContain("grant_type");
		sql.ShouldContain("qualifier");
		sql.ShouldContain("expires_on");
		sql.ShouldContain("granted_by");
		sql.ShouldContain("granted_on");
	}

	[Fact]
	public void GenerateSql_WithTimestamptzCasts()
	{
		// Arrange
		var grant = CreateTestGrant();

		// Act
		var request = new SaveGrantRequest(grant, CancellationToken.None);

		// Assert
		var sql = request.Command.CommandText;
		sql.ShouldContain("@ExpiresOn::timestamptz");
		sql.ShouldContain("@GrantedOn::timestamptz");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenGrantIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new SaveGrantRequest(
			null!, CancellationToken.None));
	}

	[Fact]
	public void SetCommandTimeout_ToRegularTimeout()
	{
		// Arrange
		var grant = CreateTestGrant();

		// Act
		var request = new SaveGrantRequest(grant, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(Excalibur.Data.Abstractions.DbTimeouts.RegularTimeoutSeconds);
	}

	[Fact]
	public void AcceptGrantWithNullOptionalFields()
	{
		// Arrange - Grant with null optional fields
		var grant = new Grant(
			UserId: "user-123",
			FullName: null,
			TenantId: null,
			GrantType: "role",
			Qualifier: "admin",
			ExpiresOn: null,
			GrantedBy: "system",
			GrantedOn: DateTimeOffset.UtcNow);

		// Act
		var request = new SaveGrantRequest(grant, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}
}
