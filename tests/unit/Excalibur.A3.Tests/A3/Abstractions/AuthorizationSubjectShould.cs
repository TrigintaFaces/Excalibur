// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.Tests.A3.Abstractions;

/// <summary>
/// Unit tests for <see cref="AuthorizationSubject"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizationSubjectShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithActorIdOnly_HasNullTenantAndAttributes()
	{
		// Arrange & Act
		var subject = new AuthorizationSubject("user-123", null, null);

		// Assert
		subject.ActorId.ShouldBe("user-123");
		subject.TenantId.ShouldBeNull();
		subject.Attributes.ShouldBeNull();
	}

	[Fact]
	public void Create_WithActorIdAndTenant_SetsValues()
	{
		// Arrange & Act
		var subject = new AuthorizationSubject("user-123", "tenant-abc", null);

		// Assert
		subject.ActorId.ShouldBe("user-123");
		subject.TenantId.ShouldBe("tenant-abc");
		subject.Attributes.ShouldBeNull();
	}

	[Fact]
	public void Create_WithAllParameters_SetsValues()
	{
		// Arrange
		var attributes = new Dictionary<string, string>
		{
			["role"] = "admin",
			["department"] = "engineering"
		};

		// Act
		var subject = new AuthorizationSubject("user-456", "tenant-xyz", attributes);

		// Assert
		subject.ActorId.ShouldBe("user-456");
		subject.TenantId.ShouldBe("tenant-xyz");
		subject.Attributes.ShouldNotBeNull();
		subject.Attributes.Count.ShouldBe(2);
		subject.Attributes["role"].ShouldBe("admin");
		subject.Attributes["department"].ShouldBe("engineering");
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equality_SameActorIdAndNulls_AreEqual()
	{
		// Arrange
		var subject1 = new AuthorizationSubject("user-123", null, null);
		var subject2 = new AuthorizationSubject("user-123", null, null);

		// Act & Assert
		subject1.ShouldBe(subject2);
	}

	[Fact]
	public void Equality_DifferentActorId_AreNotEqual()
	{
		// Arrange
		var subject1 = new AuthorizationSubject("user-123", null, null);
		var subject2 = new AuthorizationSubject("user-456", null, null);

		// Act & Assert
		subject1.ShouldNotBe(subject2);
	}

	[Fact]
	public void Equality_DifferentTenantId_AreNotEqual()
	{
		// Arrange
		var subject1 = new AuthorizationSubject("user-123", "tenant-a", null);
		var subject2 = new AuthorizationSubject("user-123", "tenant-b", null);

		// Act & Assert
		subject1.ShouldNotBe(subject2);
	}

	[Fact]
	public void Equality_NullVsNonNullTenant_AreNotEqual()
	{
		// Arrange
		var subject1 = new AuthorizationSubject("user-123", null, null);
		var subject2 = new AuthorizationSubject("user-123", "tenant-a", null);

		// Act & Assert
		subject1.ShouldNotBe(subject2);
	}

	#endregion

	#region With Expression Tests

	[Fact]
	public void With_CreatesModifiedCopy_ActorId()
	{
		// Arrange
		var original = new AuthorizationSubject("user-123", "tenant-a", null);

		// Act
		var modified = original with { ActorId = "user-456" };

		// Assert
		original.ActorId.ShouldBe("user-123");
		modified.ActorId.ShouldBe("user-456");
		modified.TenantId.ShouldBe("tenant-a");
	}

	[Fact]
	public void With_CreatesModifiedCopy_TenantId()
	{
		// Arrange
		var original = new AuthorizationSubject("user-123", "tenant-a", null);

		// Act
		var modified = original with { TenantId = "tenant-b" };

		// Assert
		original.TenantId.ShouldBe("tenant-a");
		modified.TenantId.ShouldBe("tenant-b");
	}

	[Fact]
	public void With_CreatesModifiedCopy_Attributes()
	{
		// Arrange
		var original = new AuthorizationSubject("user-123", null, null);
		var newAttrs = new Dictionary<string, string> { ["role"] = "viewer" };

		// Act
		var modified = original with { Attributes = newAttrs };

		// Assert
		original.Attributes.ShouldBeNull();
		modified.Attributes.ShouldNotBeNull();
		modified.Attributes["role"].ShouldBe("viewer");
	}

	#endregion

	#region Service Account Scenarios

	[Theory]
	[InlineData("service-account-api")]
	[InlineData("system")]
	[InlineData("background-worker")]
	public void Create_WithServiceAccountIds_Succeeds(string serviceId)
	{
		// Act
		var subject = new AuthorizationSubject(serviceId, null, null);

		// Assert
		subject.ActorId.ShouldBe(serviceId);
	}

	#endregion
}
