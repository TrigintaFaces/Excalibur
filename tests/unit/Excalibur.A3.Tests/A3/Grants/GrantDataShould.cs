// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization.Grants;

namespace Excalibur.Tests.A3.Grants;

/// <summary>
/// Unit tests for <see cref="GrantData"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class GrantDataShould : UnitTestBase
{
	[Fact]
	public void InitializeWithRequiredProperties()
	{
		// Arrange & Act
		var grantData = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		// Assert
		grantData.UserId.ShouldBe("user123");
		grantData.FullName.ShouldBe("John Doe");
		grantData.TenantId.ShouldBe("tenant1");
		grantData.GrantType.ShouldBe("Permission");
		grantData.Qualifier.ShouldBe("Read");
		grantData.GrantedBy.ShouldBe("admin");
	}

	[Fact]
	public void HaveNullExpiresOn_ByDefault()
	{
		// Arrange & Act
		var grantData = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		// Assert
		grantData.ExpiresOn.ShouldBeNull();
	}

	[Fact]
	public void HaveNullGrantedOn_ByDefault()
	{
		// Arrange & Act
		var grantData = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		// Assert
		grantData.GrantedOn.ShouldBeNull();
	}

	[Fact]
	public void SupportOptionalExpiresOn()
	{
		// Arrange
		var expirationDate = DateTimeOffset.UtcNow.AddDays(30);

		// Act
		var grantData = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin",
			ExpiresOn = expirationDate
		};

		// Assert
		grantData.ExpiresOn.ShouldBe(expirationDate);
	}

	[Fact]
	public void SupportOptionalGrantedOn()
	{
		// Arrange
		var grantedDate = DateTimeOffset.UtcNow;

		// Act
		var grantData = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin",
			GrantedOn = grantedDate
		};

		// Assert
		grantData.GrantedOn.ShouldBe(grantedDate);
	}

	[Fact]
	public void BeEqualWhenPropertiesMatch()
	{
		// Arrange
		var grantData1 = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		var grantData2 = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		// Assert
		grantData1.ShouldBe(grantData2);
	}

	[Fact]
	public void NotBeEqualWhenUserIdDiffers()
	{
		// Arrange
		var grantData1 = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		var grantData2 = new GrantData
		{
			UserId = "user456",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		// Assert
		grantData1.ShouldNotBe(grantData2);
	}

	[Fact]
	public void NotBeEqualWhenTenantIdDiffers()
	{
		// Arrange
		var grantData1 = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		var grantData2 = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant2",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		// Assert
		grantData1.ShouldNotBe(grantData2);
	}

	[Fact]
	public void NotBeEqualWhenGrantTypeDiffers()
	{
		// Arrange
		var grantData1 = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		var grantData2 = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Role",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		// Assert
		grantData1.ShouldNotBe(grantData2);
	}

	[Fact]
	public void SupportWithExpression()
	{
		// Arrange
		var original = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		// Act
		var modified = original with { Qualifier = "Write" };

		// Assert
		modified.Qualifier.ShouldBe("Write");
		modified.UserId.ShouldBe("user123");
		original.Qualifier.ShouldBe("Read");
	}

	[Fact]
	public void HaveConsistentHashCode_ForEqualRecords()
	{
		// Arrange
		var grantData1 = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		var grantData2 = new GrantData
		{
			UserId = "user123",
			FullName = "John Doe",
			TenantId = "tenant1",
			GrantType = "Permission",
			Qualifier = "Read",
			GrantedBy = "admin"
		};

		// Assert
		grantData1.GetHashCode().ShouldBe(grantData2.GetHashCode());
	}
}
