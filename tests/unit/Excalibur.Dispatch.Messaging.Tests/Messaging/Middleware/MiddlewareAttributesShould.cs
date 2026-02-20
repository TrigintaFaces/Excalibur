// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for middleware marker attributes.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MiddlewareAttributesShould
{
	#region AllowAnonymousAttribute

	[Fact]
	public void AllowAnonymous_BeAssignableToAttribute()
	{
		// Arrange & Act
		var attribute = new AllowAnonymousAttribute();

		// Assert
		attribute.ShouldBeAssignableTo<Attribute>();
	}

	[Fact]
	public void AllowAnonymous_HaveAttributeUsageForClassOnly()
	{
		// Arrange & Act
		var attributeUsage = typeof(AllowAnonymousAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void AllowAnonymous_BeRetrievableFromDecoratedClass()
	{
		// Arrange & Act
		var hasAttribute = typeof(SampleAllowAnonymousClass)
			.GetCustomAttributes(typeof(AllowAnonymousAttribute), false)
			.Any();

		// Assert
		hasAttribute.ShouldBeTrue();
	}

	#endregion

	#region BypassOutboxAttribute

	[Fact]
	public void BypassOutbox_BeAssignableToAttribute()
	{
		// Arrange & Act
		var attribute = new BypassOutboxAttribute();

		// Assert
		attribute.ShouldBeAssignableTo<Attribute>();
	}

	[Fact]
	public void BypassOutbox_HaveAttributeUsageForClassOnly()
	{
		// Arrange & Act
		var attributeUsage = typeof(BypassOutboxAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void BypassOutbox_BeRetrievableFromDecoratedClass()
	{
		// Arrange & Act
		var hasAttribute = typeof(SampleBypassOutboxClass)
			.GetCustomAttributes(typeof(BypassOutboxAttribute), false)
			.Any();

		// Assert
		hasAttribute.ShouldBeTrue();
	}

	#endregion

	#region BypassRateLimitingAttribute

	[Fact]
	public void BypassRateLimiting_BeAssignableToAttribute()
	{
		// Arrange & Act
		var attribute = new BypassRateLimitingAttribute();

		// Assert
		attribute.ShouldBeAssignableTo<Attribute>();
	}

	[Fact]
	public void BypassRateLimiting_HaveAttributeUsageForClassOnly()
	{
		// Arrange & Act
		var attributeUsage = typeof(BypassRateLimitingAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void BypassRateLimiting_BeRetrievableFromDecoratedClass()
	{
		// Arrange & Act
		var hasAttribute = typeof(SampleBypassRateLimitingClass)
			.GetCustomAttributes(typeof(BypassRateLimitingAttribute), false)
			.Any();

		// Assert
		hasAttribute.ShouldBeTrue();
	}

	#endregion

	#region BypassSanitizationAttribute

	[Fact]
	public void BypassSanitization_BeAssignableToAttribute()
	{
		// Arrange & Act
		var attribute = new BypassSanitizationAttribute();

		// Assert
		attribute.ShouldBeAssignableTo<Attribute>();
	}

	[Fact]
	public void BypassSanitization_HaveAttributeUsageForClassOnly()
	{
		// Arrange & Act
		var attributeUsage = typeof(BypassSanitizationAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void BypassSanitization_BeRetrievableFromDecoratedClass()
	{
		// Arrange & Act
		var hasAttribute = typeof(SampleBypassSanitizationClass)
			.GetCustomAttributes(typeof(BypassSanitizationAttribute), false)
			.Any();

		// Assert
		hasAttribute.ShouldBeTrue();
	}

	#endregion

	#region NoSanitizeAttribute

	[Fact]
	public void NoSanitize_BeAssignableToAttribute()
	{
		// Arrange & Act
		var attribute = new NoSanitizeAttribute();

		// Assert
		attribute.ShouldBeAssignableTo<Attribute>();
	}

	[Fact]
	public void NoSanitize_HaveAttributeUsageForPropertyOnly()
	{
		// Arrange & Act
		var attributeUsage = typeof(NoSanitizeAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Property);
	}

	[Fact]
	public void NoSanitize_BeRetrievableFromDecoratedProperty()
	{
		// Arrange & Act
		var property = typeof(SampleClassWithNoSanitizeProperty)
			.GetProperty(nameof(SampleClassWithNoSanitizeProperty.RawHtml));
		var hasAttribute = property?.GetCustomAttributes(typeof(NoSanitizeAttribute), false).Any() ?? false;

		// Assert
		hasAttribute.ShouldBeTrue();
	}

	#endregion

	#region NoTransactionAttribute

	[Fact]
	public void NoTransaction_BeAssignableToAttribute()
	{
		// Arrange & Act
		var attribute = new NoTransactionAttribute();

		// Assert
		attribute.ShouldBeAssignableTo<Attribute>();
	}

	[Fact]
	public void NoTransaction_HaveAttributeUsageForClassOnly()
	{
		// Arrange & Act
		var attributeUsage = typeof(NoTransactionAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void NoTransaction_BeRetrievableFromDecoratedClass()
	{
		// Arrange & Act
		var hasAttribute = typeof(SampleNoTransactionClass)
			.GetCustomAttributes(typeof(NoTransactionAttribute), false)
			.Any();

		// Assert
		hasAttribute.ShouldBeTrue();
	}

	#endregion

	#region RequireTransactionAttribute

	[Fact]
	public void RequireTransaction_BeAssignableToAttribute()
	{
		// Arrange & Act
		var attribute = new RequireTransactionAttribute();

		// Assert
		attribute.ShouldBeAssignableTo<Attribute>();
	}

	[Fact]
	public void RequireTransaction_HaveAttributeUsageForClassOnly()
	{
		// Arrange & Act
		var attributeUsage = typeof(RequireTransactionAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void RequireTransaction_BeRetrievableFromDecoratedClass()
	{
		// Arrange & Act
		var hasAttribute = typeof(SampleRequireTransactionClass)
			.GetCustomAttributes(typeof(RequireTransactionAttribute), false)
			.Any();

		// Assert
		hasAttribute.ShouldBeTrue();
	}

	#endregion

	#region ContractVersionAttribute

	[Fact]
	public void ContractVersion_BeAssignableToAttribute()
	{
		// Arrange & Act
		var attribute = new ContractVersionAttribute("1.0.0");

		// Assert
		attribute.ShouldBeAssignableTo<Attribute>();
	}

	[Fact]
	public void ContractVersion_HaveAttributeUsageForClassOnly()
	{
		// Arrange & Act
		var attributeUsage = typeof(ContractVersionAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void ContractVersion_StoreVersionProperty()
	{
		// Arrange & Act
		var attribute = new ContractVersionAttribute("2.1.0");

		// Assert
		attribute.Version.ShouldBe("2.1.0");
	}

	[Fact]
	public void ContractVersion_ThrowOnNullVersion()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => new ContractVersionAttribute(null!));
	}

	[Theory]
	[InlineData("1.0.0")]
	[InlineData("2.0.0-beta")]
	[InlineData("v3.0")]
	[InlineData("2024.01.15")]
	public void ContractVersion_AcceptVariousVersionFormats(string version)
	{
		// Arrange & Act
		var attribute = new ContractVersionAttribute(version);

		// Assert
		attribute.Version.ShouldBe(version);
	}

	[Fact]
	public void ContractVersion_BeRetrievableFromDecoratedClass()
	{
		// Arrange & Act
		var attribute = typeof(SampleContractVersionClass)
			.GetCustomAttributes(typeof(ContractVersionAttribute), false)
			.Cast<ContractVersionAttribute>()
			.FirstOrDefault();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Version.ShouldBe("1.0.0");
	}

	#endregion

	#region SchemaIdAttribute

	[Fact]
	public void SchemaId_BeAssignableToAttribute()
	{
		// Arrange & Act
		var attribute = new SchemaIdAttribute("com.example.schema.v1");

		// Assert
		attribute.ShouldBeAssignableTo<Attribute>();
	}

	[Fact]
	public void SchemaId_HaveAttributeUsageForClassOnly()
	{
		// Arrange & Act
		var attributeUsage = typeof(SchemaIdAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void SchemaId_StoreSchemaIdProperty()
	{
		// Arrange & Act
		var attribute = new SchemaIdAttribute("com.acme.events.order-created");

		// Assert
		attribute.SchemaId.ShouldBe("com.acme.events.order-created");
	}

	[Fact]
	public void SchemaId_ThrowOnNullSchemaId()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => new SchemaIdAttribute(null!));
	}

	[Theory]
	[InlineData("simple-id")]
	[InlineData("com.example.events.OrderCreated")]
	[InlineData("urn:example:schema:order:v1")]
	[InlineData("https://schemas.example.com/orders/v1")]
	public void SchemaId_AcceptVariousSchemaIdFormats(string schemaId)
	{
		// Arrange & Act
		var attribute = new SchemaIdAttribute(schemaId);

		// Assert
		attribute.SchemaId.ShouldBe(schemaId);
	}

	[Fact]
	public void SchemaId_BeRetrievableFromDecoratedClass()
	{
		// Arrange & Act
		var attribute = typeof(SampleSchemaIdClass)
			.GetCustomAttributes(typeof(SchemaIdAttribute), false)
			.Cast<SchemaIdAttribute>()
			.FirstOrDefault();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.SchemaId.ShouldBe("com.example.test-schema");
	}

	#endregion

	#region Sample Classes

	[AllowAnonymous]
	private sealed class SampleAllowAnonymousClass
	{
	}

	[BypassOutbox]
	private sealed class SampleBypassOutboxClass
	{
	}

	[BypassRateLimiting]
	private sealed class SampleBypassRateLimitingClass
	{
	}

	[BypassSanitization]
	private sealed class SampleBypassSanitizationClass
	{
	}

	private sealed class SampleClassWithNoSanitizeProperty
	{
		[NoSanitize]
		public string? RawHtml { get; set; }

		public string? NormalText { get; set; }
	}

	[NoTransaction]
	private sealed class SampleNoTransactionClass
	{
	}

	[RequireTransaction]
	private sealed class SampleRequireTransactionClass
	{
	}

	[ContractVersion("1.0.0")]
	private sealed class SampleContractVersionClass
	{
	}

	[SchemaId("com.example.test-schema")]
	private sealed class SampleSchemaIdClass
	{
	}

	#endregion
}
