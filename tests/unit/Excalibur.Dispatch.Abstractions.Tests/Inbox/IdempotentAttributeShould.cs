// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.Inbox;

/// <summary>
/// Unit tests for the <see cref="IdempotentAttribute"/> class.
/// Validates default property values and attribute behavior for handler idempotency configuration.
/// </summary>
/// <remarks>
/// Sprint 441 S441.6: Unit tests for declarative idempotency attribute.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class IdempotentAttributeShould : UnitTestBase
{
	#region Default Value Tests

	[Fact]
	public void HaveDefaultRetentionMinutesOf1440()
	{
		// Arrange & Act
		var attribute = new IdempotentAttribute();

		// Assert
		attribute.RetentionMinutes.ShouldBe(1440);
	}

	[Fact]
	public void HaveDefaultUseInMemoryOfFalse()
	{
		// Arrange & Act
		var attribute = new IdempotentAttribute();

		// Assert
		attribute.UseInMemory.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultStrategyOfFromHeader()
	{
		// Arrange & Act
		var attribute = new IdempotentAttribute();

		// Assert
		attribute.Strategy.ShouldBe(MessageIdStrategy.FromHeader);
	}

	[Fact]
	public void HaveDefaultHeaderNameOfMessageId()
	{
		// Arrange & Act
		var attribute = new IdempotentAttribute();

		// Assert
		attribute.HeaderName.ShouldBe("MessageId");
	}

	#endregion

	#region Property Setting Tests

	[Fact]
	public void AllowSettingRetentionMinutes()
	{
		// Arrange & Act
		var attribute = new IdempotentAttribute
		{
			RetentionMinutes = 60
		};

		// Assert
		attribute.RetentionMinutes.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingUseInMemoryToTrue()
	{
		// Arrange & Act
		var attribute = new IdempotentAttribute
		{
			UseInMemory = true
		};

		// Assert
		attribute.UseInMemory.ShouldBeTrue();
	}

	[Theory]
	[InlineData(MessageIdStrategy.FromHeader)]
	[InlineData(MessageIdStrategy.FromCorrelationId)]
	[InlineData(MessageIdStrategy.CompositeKey)]
	[InlineData(MessageIdStrategy.Custom)]
	public void AllowSettingStrategy(MessageIdStrategy strategy)
	{
		// Arrange & Act
		var attribute = new IdempotentAttribute
		{
			Strategy = strategy
		};

		// Assert
		attribute.Strategy.ShouldBe(strategy);
	}

	[Fact]
	public void AllowSettingCustomHeaderName()
	{
		// Arrange & Act
		var attribute = new IdempotentAttribute
		{
			HeaderName = "X-Custom-Message-Id"
		};

		// Assert
		attribute.HeaderName.ShouldBe("X-Custom-Message-Id");
	}

	#endregion

	#region Combined Configuration Tests

	[Fact]
	public void SupportFullConfiguration()
	{
		// Arrange & Act - All properties explicitly set
		var attribute = new IdempotentAttribute
		{
			RetentionMinutes = 120,
			UseInMemory = true,
			Strategy = MessageIdStrategy.CompositeKey,
			HeaderName = "Custom-Id"
		};

		// Assert
		attribute.RetentionMinutes.ShouldBe(120);
		attribute.UseInMemory.ShouldBeTrue();
		attribute.Strategy.ShouldBe(MessageIdStrategy.CompositeKey);
		attribute.HeaderName.ShouldBe("Custom-Id");
	}

	[Fact]
	public void SupportInMemoryWithCustomRetention()
	{
		// Arrange & Act
		var attribute = new IdempotentAttribute
		{
			UseInMemory = true,
			RetentionMinutes = 30
		};

		// Assert
		attribute.UseInMemory.ShouldBeTrue();
		attribute.RetentionMinutes.ShouldBe(30);
	}

	[Fact]
	public void SupportCorrelationIdStrategyConfiguration()
	{
		// Arrange & Act
		var attribute = new IdempotentAttribute
		{
			Strategy = MessageIdStrategy.FromCorrelationId
		};

		// Assert
		attribute.Strategy.ShouldBe(MessageIdStrategy.FromCorrelationId);
		// HeaderName should still have default even though strategy is FromCorrelationId
		attribute.HeaderName.ShouldBe("MessageId");
	}

	[Fact]
	public void SupportCustomStrategyConfiguration()
	{
		// Arrange & Act
		var attribute = new IdempotentAttribute
		{
			Strategy = MessageIdStrategy.Custom
		};

		// Assert
		attribute.Strategy.ShouldBe(MessageIdStrategy.Custom);
	}

	#endregion

	#region AttributeUsage Tests

	[Fact]
	public void BeApplicableToClassesOnly()
	{
		// Arrange & Act
		var attributeUsage = typeof(IdempotentAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault() as AttributeUsageAttribute;

		// Assert
		_ = attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void NotAllowMultipleOnSameClass()
	{
		// Arrange & Act
		var attributeUsage = typeof(IdempotentAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault() as AttributeUsageAttribute;

		// Assert
		_ = attributeUsage.ShouldNotBeNull();
		attributeUsage.AllowMultiple.ShouldBeFalse();
	}

	[Fact]
	public void BeInherited()
	{
		// Arrange & Act
		var attributeUsage = typeof(IdempotentAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault() as AttributeUsageAttribute;

		// Assert
		_ = attributeUsage.ShouldNotBeNull();
		attributeUsage.Inherited.ShouldBeTrue();
	}

	[Fact]
	public void BeSealed()
	{
		// Arrange & Act
		var type = typeof(IdempotentAttribute);

		// Assert
		type.IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void InheritFromAttribute()
	{
		// Arrange & Act
		var type = typeof(IdempotentAttribute);

		// Assert
		type.IsAssignableTo(typeof(Attribute)).ShouldBeTrue();
	}

	[Fact]
	public void BeInDispatchAbstractionsNamespace()
	{
		// Arrange & Act
		var type = typeof(IdempotentAttribute);

		// Assert
		type.Namespace.ShouldBe("Excalibur.Dispatch.Abstractions");
	}

	#endregion
}
