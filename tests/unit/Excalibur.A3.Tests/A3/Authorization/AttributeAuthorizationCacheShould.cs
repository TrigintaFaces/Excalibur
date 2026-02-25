// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Unit tests for <see cref="AttributeAuthorizationCache"/>.
/// Tests T405.2 scenarios: attribute caching, property extraction, and inheritance behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Authorization")]
public sealed class AttributeAuthorizationCacheShould
{
	private readonly AttributeAuthorizationCache _cache = new();

	#region Test Message Types

	private class MessageWithNoAttribute { }

	[RequirePermission("single.permission")]
	private class MessageWithSingleAttribute { }

	[RequirePermission("first.permission")]
	[RequirePermission("second.permission")]
	private class MessageWithMultipleAttributes { }

	[RequirePermission("base.permission")]
	private class BaseMessage { }

	private class DerivedMessage : BaseMessage { }

	[RequirePermission("derived.permission")]
	private class DerivedMessageWithOwnAttribute : BaseMessage { }

	private class MessageWithResourceId
	{
		public Guid OrderId { get; set; } = Guid.NewGuid();
		public string? CustomerId { get; set; }
	}

	#endregion

	#region GetAttributes Tests

	[Fact]
	public void GetAttributes_ReturnEmptyArray_ForTypeWithoutAttribute()
	{
		// Act
		var attrs = _cache.GetAttributes(typeof(MessageWithNoAttribute));

		// Assert
		_ = attrs.ShouldNotBeNull();
		attrs.ShouldBeEmpty();
	}

	[Fact]
	public void GetAttributes_ReturnSingleAttribute()
	{
		// Act
		var attrs = _cache.GetAttributes(typeof(MessageWithSingleAttribute));

		// Assert
		attrs.Length.ShouldBe(1);
		attrs[0].Permission.ShouldBe("single.permission");
	}

	[Fact]
	public void GetAttributes_ReturnMultipleAttributes()
	{
		// Act
		var attrs = _cache.GetAttributes(typeof(MessageWithMultipleAttributes));

		// Assert
		attrs.Length.ShouldBe(2);
		attrs.Select(a => a.Permission).ShouldContain("first.permission");
		attrs.Select(a => a.Permission).ShouldContain("second.permission");
	}

	[Fact]
	public void GetAttributes_CacheResults_PerType()
	{
		// Act - First call
		var attrs1 = _cache.GetAttributes(typeof(MessageWithSingleAttribute));

		// Act - Second call
		var attrs2 = _cache.GetAttributes(typeof(MessageWithSingleAttribute));

		// Assert - Should return same array instance (cached)
		ReferenceEquals(attrs1, attrs2).ShouldBeTrue();
	}

	[Fact]
	public void GetAttributes_RespectInheritance()
	{
		// Act - DerivedMessage has no attribute but inherits from BaseMessage
		var attrs = _cache.GetAttributes(typeof(DerivedMessage));

		// Assert
		attrs.Length.ShouldBe(1);
		attrs[0].Permission.ShouldBe("base.permission");
	}

	[Fact]
	public void GetAttributes_CombineInheritedAndOwnAttributes()
	{
		// Act - DerivedMessageWithOwnAttribute has own attribute AND inherits from BaseMessage
		var attrs = _cache.GetAttributes(typeof(DerivedMessageWithOwnAttribute));

		// Assert - Should have both base and derived permissions
		attrs.Length.ShouldBe(2);
		attrs.Select(a => a.Permission).ShouldContain("base.permission");
		attrs.Select(a => a.Permission).ShouldContain("derived.permission");
	}

	[Fact]
	public void GetAttributes_ThrowArgumentNullException_WhenMessageTypeIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _cache.GetAttributes(null!));
	}

	#endregion

	#region HasAttributes Tests

	[Fact]
	public void HasAttributes_ReturnFalse_ForTypeWithoutAttribute()
	{
		// Act
		var result = _cache.HasAttributes(typeof(MessageWithNoAttribute));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void HasAttributes_ReturnTrue_ForTypeWithAttribute()
	{
		// Act
		var result = _cache.HasAttributes(typeof(MessageWithSingleAttribute));

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region ExtractResourceId Tests

	[Fact]
	public void ExtractResourceId_ReturnNull_WhenPropertyNameIsNull()
	{
		// Arrange
		var message = new MessageWithResourceId();

		// Act
		var result = _cache.ExtractResourceId(message, null);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ExtractResourceId_ReturnNull_WhenPropertyNameIsEmpty()
	{
		// Arrange
		var message = new MessageWithResourceId();

		// Act
		var result = _cache.ExtractResourceId(message, "");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ExtractResourceId_ReturnNull_WhenPropertyDoesNotExist()
	{
		// Arrange
		var message = new MessageWithResourceId();

		// Act
		var result = _cache.ExtractResourceId(message, "NonExistentProperty");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ExtractResourceId_ReturnPropertyValue_AsString()
	{
		// Arrange
		var expectedId = Guid.NewGuid();
		var message = new MessageWithResourceId { OrderId = expectedId };

		// Act
		var result = _cache.ExtractResourceId(message, "OrderId");

		// Assert
		result.ShouldBe(expectedId.ToString());
	}

	[Fact]
	public void ExtractResourceId_ReturnNull_WhenPropertyValueIsNull()
	{
		// Arrange
		var message = new MessageWithResourceId { CustomerId = null };

		// Act
		var result = _cache.ExtractResourceId(message, "CustomerId");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ExtractResourceId_ReturnStringValue_WhenPropertyIsString()
	{
		// Arrange
		var message = new MessageWithResourceId { CustomerId = "CUST-123" };

		// Act
		var result = _cache.ExtractResourceId(message, "CustomerId");

		// Assert
		result.ShouldBe("CUST-123");
	}

	[Fact]
	public void ExtractResourceId_CachePropertyInfo()
	{
		// Arrange
		var message1 = new MessageWithResourceId { OrderId = Guid.NewGuid() };
		var message2 = new MessageWithResourceId { OrderId = Guid.NewGuid() };

		// Act - Two extractions with same property name
		var result1 = _cache.ExtractResourceId(message1, "OrderId");
		var result2 = _cache.ExtractResourceId(message2, "OrderId");

		// Assert - Both should work (cache is being used internally)
		result1.ShouldBe(message1.OrderId.ToString());
		result2.ShouldBe(message2.OrderId.ToString());
	}

	[Fact]
	public void ExtractResourceId_ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _cache.ExtractResourceId(null!, "OrderId"));
	}

	#endregion
}
