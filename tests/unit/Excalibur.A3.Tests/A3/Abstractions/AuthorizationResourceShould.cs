// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.Tests.A3.Abstractions;

/// <summary>
/// Unit tests for <see cref="AuthorizationResource"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizationResourceShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithTypeAndIdOnly_HasNullAttributes()
	{
		// Arrange & Act
		var resource = new AuthorizationResource("Order", "order-123", null);

		// Assert
		resource.Type.ShouldBe("Order");
		resource.Id.ShouldBe("order-123");
		resource.Attributes.ShouldBeNull();
	}

	[Fact]
	public void Create_WithAllParameters_SetsValues()
	{
		// Arrange
		var attributes = new Dictionary<string, string>
		{
			["owner"] = "user-456",
			["status"] = "pending"
		};

		// Act
		var resource = new AuthorizationResource("Document", "doc-789", attributes);

		// Assert
		resource.Type.ShouldBe("Document");
		resource.Id.ShouldBe("doc-789");
		resource.Attributes.ShouldNotBeNull();
		resource.Attributes.Count.ShouldBe(2);
		resource.Attributes["owner"].ShouldBe("user-456");
		resource.Attributes["status"].ShouldBe("pending");
	}

	[Fact]
	public void Create_WithEmptyAttributes_SetsEmptyDictionary()
	{
		// Arrange
		var attributes = new Dictionary<string, string>();

		// Act
		var resource = new AuthorizationResource("File", "file-001", attributes);

		// Assert
		resource.Type.ShouldBe("File");
		resource.Attributes.ShouldNotBeNull();
		resource.Attributes.Count.ShouldBe(0);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equality_SameTypeAndId_AreEqual()
	{
		// Arrange
		var resource1 = new AuthorizationResource("Order", "order-123", null);
		var resource2 = new AuthorizationResource("Order", "order-123", null);

		// Act & Assert
		resource1.ShouldBe(resource2);
	}

	[Fact]
	public void Equality_DifferentType_AreNotEqual()
	{
		// Arrange
		var resource1 = new AuthorizationResource("Order", "123", null);
		var resource2 = new AuthorizationResource("Invoice", "123", null);

		// Act & Assert
		resource1.ShouldNotBe(resource2);
	}

	[Fact]
	public void Equality_DifferentId_AreNotEqual()
	{
		// Arrange
		var resource1 = new AuthorizationResource("Order", "order-123", null);
		var resource2 = new AuthorizationResource("Order", "order-456", null);

		// Act & Assert
		resource1.ShouldNotBe(resource2);
	}

	#endregion

	#region With Expression Tests

	[Fact]
	public void With_CreatesModifiedCopy_Type()
	{
		// Arrange
		var original = new AuthorizationResource("Order", "123", null);

		// Act
		var modified = original with { Type = "Invoice" };

		// Assert
		original.Type.ShouldBe("Order");
		modified.Type.ShouldBe("Invoice");
		modified.Id.ShouldBe("123");
	}

	[Fact]
	public void With_CreatesModifiedCopy_Id()
	{
		// Arrange
		var original = new AuthorizationResource("Order", "123", null);

		// Act
		var modified = original with { Id = "456" };

		// Assert
		original.Id.ShouldBe("123");
		modified.Id.ShouldBe("456");
	}

	[Fact]
	public void With_CreatesModifiedCopy_Attributes()
	{
		// Arrange
		var original = new AuthorizationResource("Order", "123", null);
		var newAttrs = new Dictionary<string, string> { ["label"] = "urgent" };

		// Act
		var modified = original with { Attributes = newAttrs };

		// Assert
		original.Attributes.ShouldBeNull();
		modified.Attributes.ShouldNotBeNull();
		modified.Attributes["label"].ShouldBe("urgent");
	}

	#endregion

	#region Common Resource Types

	[Theory]
	[InlineData("Order")]
	[InlineData("Document")]
	[InlineData("User")]
	[InlineData("Account")]
	[InlineData("Product")]
	[InlineData("Invoice")]
	public void Create_WithCommonResourceTypes_Succeeds(string resourceType)
	{
		// Act
		var resource = new AuthorizationResource(resourceType, "id-123", null);

		// Assert
		resource.Type.ShouldBe(resourceType);
	}

	#endregion
}
