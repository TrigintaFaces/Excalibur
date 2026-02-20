// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for the <see cref="AutoRegisterAttribute"/> class.
/// Validates default property values and attribute behavior for service registration configuration.
/// </summary>
/// <remarks>
/// Sprint 426 T426.6: Unit tests for AutoRegister attribute and generator.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AutoRegisterAttributeShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultLifetimeOfScoped()
	{
		// Arrange & Act
		var attribute = new AutoRegisterAttribute();

		// Assert
		attribute.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void HaveAsSelfDefaultOfTrue()
	{
		// Arrange & Act
		var attribute = new AutoRegisterAttribute();

		// Assert
		attribute.AsSelf.ShouldBeTrue();
	}

	[Fact]
	public void HaveAsInterfacesDefaultOfTrue()
	{
		// Arrange & Act
		var attribute = new AutoRegisterAttribute();

		// Assert
		attribute.AsInterfaces.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingLifetimeToSingleton()
	{
		// Arrange & Act
		var attribute = new AutoRegisterAttribute
		{
			Lifetime = ServiceLifetime.Singleton
		};

		// Assert
		attribute.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AllowSettingLifetimeToTransient()
	{
		// Arrange & Act
		var attribute = new AutoRegisterAttribute
		{
			Lifetime = ServiceLifetime.Transient
		};

		// Assert
		attribute.Lifetime.ShouldBe(ServiceLifetime.Transient);
	}

	[Fact]
	public void AllowDisablingAsSelf()
	{
		// Arrange & Act
		var attribute = new AutoRegisterAttribute
		{
			AsSelf = false
		};

		// Assert
		attribute.AsSelf.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingAsInterfaces()
	{
		// Arrange & Act
		var attribute = new AutoRegisterAttribute
		{
			AsInterfaces = false
		};

		// Assert
		attribute.AsInterfaces.ShouldBeFalse();
	}

	[Fact]
	public void SupportCombinedAsSelfOnlyConfiguration()
	{
		// Arrange & Act - AsSelf only (no interface registration)
		var attribute = new AutoRegisterAttribute
		{
			AsSelf = true,
			AsInterfaces = false
		};

		// Assert
		attribute.AsSelf.ShouldBeTrue();
		attribute.AsInterfaces.ShouldBeFalse();
	}

	[Fact]
	public void SupportCombinedAsInterfacesOnlyConfiguration()
	{
		// Arrange & Act - AsInterfaces only (no concrete type registration)
		var attribute = new AutoRegisterAttribute
		{
			AsSelf = false,
			AsInterfaces = true
		};

		// Assert
		attribute.AsSelf.ShouldBeFalse();
		attribute.AsInterfaces.ShouldBeTrue();
	}

	[Fact]
	public void SupportFullConfiguration()
	{
		// Arrange & Act - All properties explicitly set
		var attribute = new AutoRegisterAttribute
		{
			Lifetime = ServiceLifetime.Singleton,
			AsSelf = true,
			AsInterfaces = true
		};

		// Assert
		attribute.Lifetime.ShouldBe(ServiceLifetime.Singleton);
		attribute.AsSelf.ShouldBeTrue();
		attribute.AsInterfaces.ShouldBeTrue();
	}

	[Fact]
	public void BeApplicableToClassesOnly()
	{
		// Arrange & Act
		var attributeUsage = typeof(AutoRegisterAttribute)
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
		var attributeUsage = typeof(AutoRegisterAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault() as AttributeUsageAttribute;

		// Assert
		_ = attributeUsage.ShouldNotBeNull();
		attributeUsage.AllowMultiple.ShouldBeFalse();
	}

	[Fact]
	public void NotBeInherited()
	{
		// Arrange & Act
		var attributeUsage = typeof(AutoRegisterAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault() as AttributeUsageAttribute;

		// Assert
		_ = attributeUsage.ShouldNotBeNull();
		attributeUsage.Inherited.ShouldBeFalse();
	}

	[Fact]
	public void BeSealed()
	{
		// Arrange & Act
		var type = typeof(AutoRegisterAttribute);

		// Assert
		type.IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void InheritFromAttribute()
	{
		// Arrange & Act
		var type = typeof(AutoRegisterAttribute);

		// Assert
		type.IsAssignableTo(typeof(Attribute)).ShouldBeTrue();
	}

	[Fact]
	public void BeInDispatchAbstractionsNamespace()
	{
		// Arrange & Act
		var type = typeof(AutoRegisterAttribute);

		// Assert
		type.Namespace.ShouldBe("Excalibur.Dispatch.Abstractions");
	}
}
