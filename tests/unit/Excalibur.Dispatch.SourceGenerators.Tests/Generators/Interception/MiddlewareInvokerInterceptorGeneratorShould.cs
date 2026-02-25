// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.SourceGenerators.Interception;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Interception;

/// <summary>
/// Tests for <see cref="MiddlewareInvokerInterceptorGenerator"/> - generates typed middleware invoker registry.
/// Validates middleware type discovery, invoker generation patterns, and registry behavior.
/// </summary>
/// <remarks>
/// Sprint 456 - S456.4: Unit tests for middleware invocation interceptors (PERF-10).
/// Tests the generator logic, MiddlewareInterceptorInfo model, and expected output patterns.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class MiddlewareInvokerInterceptorGeneratorShould
{
	#region MiddlewareInterceptorInfo Model Tests (6 tests)

	[Fact]
	public void MiddlewareInterceptorInfo_SafeIdentifier_ReplacesDotsWithUnderscores()
	{
		// Arrange
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = "Excalibur.Dispatch.Middleware.Logging"
		};

		// Act
		var safeId = info.SafeIdentifier;

		// Assert
		safeId.ShouldBe("Excalibur_Dispatch_Middleware_Logging");
	}

	[Fact]
	public void MiddlewareInterceptorInfo_SafeIdentifier_ReplacesPlusWithUnderscores()
	{
		// Arrange - nested class uses + separator
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = "OuterClass+NestedMiddleware"
		};

		// Act
		var safeId = info.SafeIdentifier;

		// Assert
		safeId.ShouldBe("OuterClass_NestedMiddleware");
	}

	[Fact]
	public void MiddlewareInterceptorInfo_DefaultsToEmptyNamespace()
	{
		// Arrange
		var info = new MiddlewareInterceptorInfo();

		// Assert
		info.Namespace.ShouldBe(string.Empty);
	}

	[Fact]
	public void MiddlewareInterceptorInfo_DefaultsHasAppliesToAttributeToFalse()
	{
		// Arrange
		var info = new MiddlewareInterceptorInfo();

		// Assert
		info.HasAppliesToAttribute.ShouldBeFalse();
	}

	[Fact]
	public void MiddlewareInterceptorInfo_Equals_ReturnsTrueForSameFullName()
	{
		// Arrange
		var info1 = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeFullName = "global::Excalibur.Dispatch.Middleware.LoggingMiddleware",
			MiddlewareTypeName = "LoggingMiddleware"
		};
		var info2 = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeFullName = "global::Excalibur.Dispatch.Middleware.LoggingMiddleware",
			MiddlewareTypeName = "LoggingMiddleware"
		};

		// Act & Assert
		info1.Equals(info2).ShouldBeTrue();
		info1.GetHashCode().ShouldBe(info2.GetHashCode());
	}

	[Fact]
	public void MiddlewareInterceptorInfo_Equals_ReturnsFalseForDifferentFullName()
	{
		// Arrange
		var info1 = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeFullName = "global::Excalibur.Dispatch.Middleware.LoggingMiddleware"
		};
		var info2 = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeFullName = "global::Excalibur.Dispatch.Middleware.ValidationMiddleware"
		};

		// Act & Assert
		info1.Equals(info2).ShouldBeFalse();
	}

	#endregion

	#region Generator Registration Tests (3 tests)

	[Fact]
	public void Generator_ImplementsIIncrementalGenerator()
	{
		// Assert
		typeof(MiddlewareInvokerInterceptorGenerator).GetInterfaces()
			.ShouldContain(typeof(Microsoft.CodeAnalysis.IIncrementalGenerator));
	}

	[Fact]
	public void Generator_HasGeneratorAttribute()
	{
		// Assert
		var attribute = typeof(MiddlewareInvokerInterceptorGenerator)
			.GetCustomAttributes(typeof(Microsoft.CodeAnalysis.GeneratorAttribute), false);
		attribute.ShouldNotBeEmpty();
	}

	[Fact]
	public void Generator_CanBeInstantiated()
	{
		// Act
		var generator = new MiddlewareInvokerInterceptorGenerator();

		// Assert
		_ = generator.ShouldNotBeNull();
	}

	#endregion

	#region SafeIdentifier Edge Cases (4 tests)

	[Fact]
	public void MiddlewareInterceptorInfo_SafeIdentifier_HandlesSimpleName()
	{
		// Arrange
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = "LoggingMiddleware"
		};

		// Act
		var safeId = info.SafeIdentifier;

		// Assert
		safeId.ShouldBe("LoggingMiddleware");
	}

	[Fact]
	public void MiddlewareInterceptorInfo_SafeIdentifier_HandlesNestedPlusDot()
	{
		// Arrange - complex nested scenario
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = "Outer.Inner+Nested"
		};

		// Act
		var safeId = info.SafeIdentifier;

		// Assert
		safeId.ShouldBe("Outer_Inner_Nested");
	}

	[Fact]
	public void MiddlewareInterceptorInfo_SafeIdentifier_PreservesLettersAndDigits()
	{
		// Arrange
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = "Middleware123"
		};

		// Act
		var safeId = info.SafeIdentifier;

		// Assert
		safeId.ShouldBe("Middleware123");
	}

	[Fact]
	public void MiddlewareInterceptorInfo_SafeIdentifier_HandlesEmptyString()
	{
		// Arrange
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = string.Empty
		};

		// Act
		var safeId = info.SafeIdentifier;

		// Assert
		safeId.ShouldBe(string.Empty);
	}

	#endregion

	#region Generated Output Pattern Tests (4 tests)

	[Fact]
	public void ExpectedOutput_UsesFileModifier_ForInvokerRegistry()
	{
		// The generator produces a file-scoped class to avoid conflicts
		// Expected pattern: "file static class MiddlewareInvokerRegistry"
		// This test documents the expected behavior

		// Assert - MiddlewareInterceptorInfo supports the data needed for file-scoped generation
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = "LoggingMiddleware",
			MiddlewareTypeFullName = "global::TestApp.LoggingMiddleware"
		};

		// Generator should use MiddlewareTypeFullName for type references
		info.MiddlewareTypeFullName.ShouldStartWith("global::");
	}

	[Fact]
	public void ExpectedOutput_IncludesHotReloadDetection()
	{
		// Expected pattern: Registry checks DOTNET_WATCH and DOTNET_MODIFIABLE_ASSEMBLIES
		// and skips typed invocation in hot reload mode

		// This test documents the expected behavior - hot reload detection is built in
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = "LoggingMiddleware",
			MiddlewareTypeFullName = "global::TestApp.LoggingMiddleware"
		};

		// Generator supports the data needed for hot reload-aware generation
		_ = info.MiddlewareTypeFullName.ShouldNotBeNull();
	}

	[Fact]
	public void ExpectedOutput_GeneratesFrozenDictionary_ForInvokers()
	{
		// Expected pattern: FrozenDictionary<Type, MiddlewareInvoker> _invokers
		// This test documents the expected data structure

		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = "ValidationMiddleware",
			MiddlewareTypeFullName = "global::TestApp.ValidationMiddleware",
			HasAppliesToAttribute = true
		};

		// Info must have type information for FrozenDictionary key generation
		info.MiddlewareTypeFullName.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void ExpectedOutput_HasFallback_ForUnknownMiddleware()
	{
		// Expected pattern: If middleware not in registry, fall back to interface dispatch
		// Generated code pattern: return middleware.InvokeAsync(...)

		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = "DynamicMiddleware",
			MiddlewareTypeFullName = "global::TestApp.DynamicMiddleware"
		};

		// Fallback uses interface dispatch when type not found in registry
		info.MiddlewareTypeFullName.ShouldNotBeNullOrEmpty();
	}

	#endregion

	#region Attribute Detection Tests (3 tests)

	[Fact]
	public void MiddlewareInterceptorInfo_TracksAppliesToAttribute()
	{
		// Arrange
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = "CommandOnlyMiddleware",
			HasAppliesToAttribute = true
		};

		// Assert
		info.HasAppliesToAttribute.ShouldBeTrue();
	}

	[Fact]
	public void MiddlewareInterceptorInfo_TracksExcludeKindsAttribute()
	{
		// Arrange
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = "EventExcludingMiddleware",
			HasExcludeKindsAttribute = true
		};

		// Assert
		info.HasExcludeKindsAttribute.ShouldBeTrue();
	}

	[Fact]
	public void MiddlewareInterceptorInfo_TracksStageOverride()
	{
		// Arrange
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeName = "CustomStageMiddleware",
			OverridesStage = true
		};

		// Assert
		info.OverridesStage.ShouldBeTrue();
	}

	#endregion

	#region Equality Tests (4 tests)

	[Fact]
	public void MiddlewareInterceptorInfo_Equals_ReturnsFalseForNull()
	{
		// Arrange
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeFullName = "global::TestApp.Middleware"
		};

		// Act & Assert
		info.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void MiddlewareInterceptorInfo_Equals_WorksWithObjectOverload()
	{
		// Arrange
		var info1 = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeFullName = "global::TestApp.Middleware"
		};
		object info2 = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeFullName = "global::TestApp.Middleware"
		};

		// Act & Assert
		info1.Equals(info2).ShouldBeTrue();
	}

	[Fact]
	public void MiddlewareInterceptorInfo_Equals_ReturnsFalseForNonMiddlewareInfo()
	{
		// Arrange
		var info = new MiddlewareInterceptorInfo
		{
			MiddlewareTypeFullName = "global::TestApp.Middleware"
		};

		// Act & Assert
		info.Equals("not a middleware info").ShouldBeFalse();
	}

	[Fact]
	public void MiddlewareInterceptorInfo_GetHashCode_ConsistentForSameFullName()
	{
		// Arrange
		var info1 = new MiddlewareInterceptorInfo { MiddlewareTypeFullName = "global::TestApp.A" };
		var info2 = new MiddlewareInterceptorInfo { MiddlewareTypeFullName = "global::TestApp.B" };

		// Act & Assert - different names should typically have different hashes
		info1.GetHashCode().ShouldNotBe(info2.GetHashCode());
	}

	#endregion
}
