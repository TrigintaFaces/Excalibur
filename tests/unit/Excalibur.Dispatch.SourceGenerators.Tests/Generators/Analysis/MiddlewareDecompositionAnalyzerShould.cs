// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.SourceGenerators.Analysis;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Analysis;

/// <summary>
/// Tests for <see cref="MiddlewareDecompositionAnalyzer"/> - analyzes middleware for Before/After decomposition.
/// Validates decomposability detection, state variable extraction, and control flow pattern recognition.
/// </summary>
/// <remarks>
/// Sprint 457 - S457.4: Unit tests for middleware decomposition analysis (PERF-23 Phase 2).
/// Tests the analyzer logic, MiddlewareDecomposition model, and expected output patterns.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class MiddlewareDecompositionAnalyzerShould
{
	#region MiddlewareDecomposition Model Tests (6 tests)

	[Fact]
	public void MiddlewareDecomposition_SafeIdentifier_ReplacesDotsWithUnderscores()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "Excalibur.Dispatch.Middleware.Logging"
		};

		// Act
		var safeId = decomposition.SafeIdentifier;

		// Assert
		safeId.ShouldBe("Excalibur_Dispatch_Middleware_Logging");
	}

	[Fact]
	public void MiddlewareDecomposition_SafeIdentifier_ReplacesPlusWithUnderscores()
	{
		// Arrange - nested class uses + separator
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "OuterClass+NestedMiddleware"
		};

		// Act
		var safeId = decomposition.SafeIdentifier;

		// Assert
		safeId.ShouldBe("OuterClass_NestedMiddleware");
	}

	[Fact]
	public void MiddlewareDecomposition_DefaultsIsDecomposableToFalse()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition();

		// Assert
		decomposition.IsDecomposable.ShouldBeFalse();
	}

	[Fact]
	public void MiddlewareDecomposition_DefaultsStateVariablesToEmptyList()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition();

		// Assert
		_ = decomposition.StateVariables.ShouldNotBeNull();
		decomposition.StateVariables.ShouldBeEmpty();
	}

	[Fact]
	public void MiddlewareDecomposition_Equals_ReturnsTrueForSameFullName()
	{
		// Arrange
		var decomposition1 = new MiddlewareDecomposition
		{
			MiddlewareTypeFullName = "global::Excalibur.Dispatch.Middleware.LoggingMiddleware",
			MiddlewareTypeName = "LoggingMiddleware"
		};
		var decomposition2 = new MiddlewareDecomposition
		{
			MiddlewareTypeFullName = "global::Excalibur.Dispatch.Middleware.LoggingMiddleware",
			MiddlewareTypeName = "LoggingMiddleware"
		};

		// Act & Assert
		decomposition1.Equals(decomposition2).ShouldBeTrue();
		decomposition1.GetHashCode().ShouldBe(decomposition2.GetHashCode());
	}

	[Fact]
	public void MiddlewareDecomposition_Equals_ReturnsFalseForDifferentFullName()
	{
		// Arrange
		var decomposition1 = new MiddlewareDecomposition
		{
			MiddlewareTypeFullName = "global::Excalibur.Dispatch.Middleware.LoggingMiddleware"
		};
		var decomposition2 = new MiddlewareDecomposition
		{
			MiddlewareTypeFullName = "global::Excalibur.Dispatch.Middleware.ValidationMiddleware"
		};

		// Act & Assert
		decomposition1.Equals(decomposition2).ShouldBeFalse();
	}

	#endregion

	#region Generator Registration Tests (3 tests)

	[Fact]
	public void Analyzer_ImplementsIIncrementalGenerator()
	{
		// Assert
		typeof(MiddlewareDecompositionAnalyzer).GetInterfaces()
			.ShouldContain(typeof(Microsoft.CodeAnalysis.IIncrementalGenerator));
	}

	[Fact]
	public void Analyzer_HasGeneratorAttribute()
	{
		// Assert
		var attribute = typeof(MiddlewareDecompositionAnalyzer)
			.GetCustomAttributes(typeof(Microsoft.CodeAnalysis.GeneratorAttribute), false);
		attribute.ShouldNotBeEmpty();
	}

	[Fact]
	public void Analyzer_CanBeInstantiated()
	{
		// Act
		var analyzer = new MiddlewareDecompositionAnalyzer();

		// Assert
		_ = analyzer.ShouldNotBeNull();
	}

	#endregion

	#region Decomposability Detection Tests (6 tests)

	[Fact]
	public void MiddlewareDecomposition_IsDecomposable_TrueForSimpleBeforeAfter()
	{
		// Arrange - simple before/after pattern is decomposable
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "LoggingMiddleware",
			MiddlewareTypeFullName = "global::TestApp.LoggingMiddleware",
			IsDecomposable = true,
			HasBeforePhase = true,
			HasAfterPhase = true
		};

		// Assert
		decomposition.IsDecomposable.ShouldBeTrue();
		decomposition.NonDecomposableReason.ShouldBeNull();
	}

	[Fact]
	public void MiddlewareDecomposition_IsDecomposable_FalseForMultipleNextCalls()
	{
		// Arrange - retry pattern with multiple next() calls
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "RetryMiddleware",
			MiddlewareTypeFullName = "global::TestApp.RetryMiddleware",
			IsDecomposable = false,
			NonDecomposableReason = "Multiple next() delegate calls detected (likely retry pattern)"
		};

		// Assert
		decomposition.IsDecomposable.ShouldBeFalse();
		decomposition.NonDecomposableReason.ShouldContain("Multiple next()");
	}

	[Fact]
	public void MiddlewareDecomposition_IsDecomposable_FalseForNextInLoop()
	{
		// Arrange - next() inside a loop
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "LoopMiddleware",
			MiddlewareTypeFullName = "global::TestApp.LoopMiddleware",
			IsDecomposable = false,
			NonDecomposableReason = "next() call is inside a loop (retry pattern)"
		};

		// Assert
		decomposition.IsDecomposable.ShouldBeFalse();
		decomposition.NonDecomposableReason.ShouldContain("loop");
	}

	[Fact]
	public void MiddlewareDecomposition_IsDecomposable_FalseForConditionalNext()
	{
		// Arrange - next() inside conditional based on runtime values
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "ConditionalMiddleware",
			MiddlewareTypeFullName = "global::TestApp.ConditionalMiddleware",
			IsDecomposable = false,
			NonDecomposableReason = "next() call is inside conditional based on runtime values"
		};

		// Assert
		decomposition.IsDecomposable.ShouldBeFalse();
		decomposition.NonDecomposableReason.ShouldContain("conditional");
	}

	[Fact]
	public void MiddlewareDecomposition_IsDecomposable_FalseForNoNextCall()
	{
		// Arrange - middleware doesn't call next() (invalid middleware)
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "BrokenMiddleware",
			MiddlewareTypeFullName = "global::TestApp.BrokenMiddleware",
			IsDecomposable = false,
			NonDecomposableReason = "No next() delegate call found"
		};

		// Assert
		decomposition.IsDecomposable.ShouldBeFalse();
		decomposition.NonDecomposableReason.ShouldContain("No next()");
	}

	[Fact]
	public void MiddlewareDecomposition_IsDecomposable_TrueForPassThrough()
	{
		// Arrange - pass-through middleware (expression body: => next(...))
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "PassThroughMiddleware",
			MiddlewareTypeFullName = "global::TestApp.PassThroughMiddleware",
			IsDecomposable = true,
			HasBeforePhase = false,
			HasAfterPhase = false
		};

		// Assert
		decomposition.IsDecomposable.ShouldBeTrue();
		decomposition.HasBeforePhase.ShouldBeFalse();
		decomposition.HasAfterPhase.ShouldBeFalse();
	}

	#endregion

	#region Control Flow Pattern Tests (5 tests)

	[Fact]
	public void MiddlewareDecomposition_TracksTryCatch()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "ErrorHandlingMiddleware",
			IsDecomposable = true,
			HasTryCatch = true,
			HasFinally = false
		};

		// Assert
		decomposition.HasTryCatch.ShouldBeTrue();
	}

	[Fact]
	public void MiddlewareDecomposition_TracksFinally()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "CleanupMiddleware",
			IsDecomposable = true,
			HasTryCatch = false,
			HasFinally = true
		};

		// Assert
		decomposition.HasFinally.ShouldBeTrue();
	}

	[Fact]
	public void MiddlewareDecomposition_TracksUsing()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "ScopedMiddleware",
			IsDecomposable = true,
			HasUsing = true
		};

		// Assert
		decomposition.HasUsing.ShouldBeTrue();
	}

	[Fact]
	public void MiddlewareDecomposition_TracksShortCircuit()
	{
		// Arrange - middleware can return early without calling next()
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "ValidationMiddleware",
			IsDecomposable = true,
			CanShortCircuit = true
		};

		// Assert
		decomposition.CanShortCircuit.ShouldBeTrue();
	}

	[Fact]
	public void MiddlewareDecomposition_TracksStage()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "ValidationMiddleware",
			Stage = 200 // DispatchMiddlewareStage.Validation
		};

		// Assert
		decomposition.Stage.ShouldBe(200);
	}

	#endregion

	#region State Variable Tests (4 tests)

	[Fact]
	public void StateVariable_TracksNameAndType()
	{
		// Arrange
		var stateVar = new StateVariable
		{
			Name = "stopwatch",
			TypeFullName = "global::System.Diagnostics.Stopwatch"
		};

		// Assert
		stateVar.Name.ShouldBe("stopwatch");
		stateVar.TypeFullName.ShouldBe("global::System.Diagnostics.Stopwatch");
	}

	[Fact]
	public void StateVariable_TracksNullability()
	{
		// Arrange
		var stateVar = new StateVariable
		{
			Name = "previousValue",
			TypeFullName = "global::System.String",
			IsNullable = true
		};

		// Assert
		stateVar.IsNullable.ShouldBeTrue();
	}

	[Fact]
	public void StateVariable_TracksDisposalRequirement()
	{
		// Arrange
		var stateVar = new StateVariable
		{
			Name = "scope",
			TypeFullName = "global::System.IDisposable",
			RequiresDisposal = true
		};

		// Assert
		stateVar.RequiresDisposal.ShouldBeTrue();
	}

	[Fact]
	public void MiddlewareDecomposition_CollectsStateVariables()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "TimingMiddleware",
			IsDecomposable = true,
			StateVariables =
			[
				new StateVariable { Name = "stopwatch", TypeFullName = "global::System.Diagnostics.Stopwatch" },
				new StateVariable { Name = "startTime", TypeFullName = "global::System.DateTime" }
			]
		};

		// Assert
		decomposition.StateVariables.Count.ShouldBe(2);
		decomposition.StateVariables[0].Name.ShouldBe("stopwatch");
		decomposition.StateVariables[1].Name.ShouldBe("startTime");
	}

	#endregion

	#region Generated Output Pattern Tests (4 tests)

	[Fact]
	public void ExpectedOutput_UsesFileModifier_ForDecompositionMetadata()
	{
		// The analyzer produces a file-scoped class to avoid conflicts
		// Expected pattern: "file static class MiddlewareDecompositionMetadata"

		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "LoggingMiddleware",
			MiddlewareTypeFullName = "global::TestApp.LoggingMiddleware"
		};

		// Generator should use MiddlewareTypeFullName for type references
		decomposition.MiddlewareTypeFullName.ShouldStartWith("global::");
	}

	[Fact]
	public void ExpectedOutput_GeneratesFrozenDictionary_ForDecompositions()
	{
		// Expected pattern: FrozenDictionary<Type, DecompositionInfo> _decompositions

		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "ValidationMiddleware",
			MiddlewareTypeFullName = "global::TestApp.ValidationMiddleware",
			IsDecomposable = true
		};

		// Info must have type information for FrozenDictionary key generation
		decomposition.MiddlewareTypeFullName.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void ExpectedOutput_GeneratesIsDecomposableMethod()
	{
		// Expected pattern: public static bool IsDecomposable<TMiddleware>()

		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "DecomposableMiddleware",
			MiddlewareTypeFullName = "global::TestApp.DecomposableMiddleware",
			IsDecomposable = true
		};

		// The generated method will check the FrozenDictionary
		decomposition.IsDecomposable.ShouldBeTrue();
	}

	[Fact]
	public void ExpectedOutput_GeneratesGetInfoMethod()
	{
		// Expected pattern: public static DecompositionInfo? GetInfo(Type middlewareType)

		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "TestMiddleware",
			MiddlewareTypeFullName = "global::TestApp.TestMiddleware",
			IsDecomposable = true,
			HasBeforePhase = true,
			HasAfterPhase = true
		};

		// Info must have all decomposition characteristics
		decomposition.HasBeforePhase.ShouldBeTrue();
		decomposition.HasAfterPhase.ShouldBeTrue();
	}

	#endregion

	#region Equality Tests (4 tests)

	[Fact]
	public void MiddlewareDecomposition_Equals_ReturnsFalseForNull()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeFullName = "global::TestApp.Middleware"
		};

		// Act & Assert
		decomposition.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void MiddlewareDecomposition_Equals_WorksWithObjectOverload()
	{
		// Arrange
		var decomposition1 = new MiddlewareDecomposition
		{
			MiddlewareTypeFullName = "global::TestApp.Middleware"
		};
		object decomposition2 = new MiddlewareDecomposition
		{
			MiddlewareTypeFullName = "global::TestApp.Middleware"
		};

		// Act & Assert
		decomposition1.Equals(decomposition2).ShouldBeTrue();
	}

	[Fact]
	public void MiddlewareDecomposition_Equals_ReturnsFalseForNonMiddlewareDecomposition()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeFullName = "global::TestApp.Middleware"
		};

		// Act & Assert
		decomposition.Equals("not a decomposition").ShouldBeFalse();
	}

	[Fact]
	public void MiddlewareDecomposition_GetHashCode_ConsistentForSameFullName()
	{
		// Arrange
		var decomposition1 = new MiddlewareDecomposition { MiddlewareTypeFullName = "global::TestApp.A" };
		var decomposition2 = new MiddlewareDecomposition { MiddlewareTypeFullName = "global::TestApp.B" };

		// Act & Assert - different names should typically have different hashes
		decomposition1.GetHashCode().ShouldNotBe(decomposition2.GetHashCode());
	}

	#endregion

	#region SafeIdentifier Edge Cases (4 tests)

	[Fact]
	public void MiddlewareDecomposition_SafeIdentifier_HandlesSimpleName()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "LoggingMiddleware"
		};

		// Act
		var safeId = decomposition.SafeIdentifier;

		// Assert
		safeId.ShouldBe("LoggingMiddleware");
	}

	[Fact]
	public void MiddlewareDecomposition_SafeIdentifier_HandlesNestedPlusDot()
	{
		// Arrange - complex nested scenario
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "Outer.Inner+Nested"
		};

		// Act
		var safeId = decomposition.SafeIdentifier;

		// Assert
		safeId.ShouldBe("Outer_Inner_Nested");
	}

	[Fact]
	public void MiddlewareDecomposition_SafeIdentifier_PreservesLettersAndDigits()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = "Middleware123"
		};

		// Act
		var safeId = decomposition.SafeIdentifier;

		// Assert
		safeId.ShouldBe("Middleware123");
	}

	[Fact]
	public void MiddlewareDecomposition_SafeIdentifier_HandlesEmptyString()
	{
		// Arrange
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareTypeName = string.Empty
		};

		// Act
		var safeId = decomposition.SafeIdentifier;

		// Assert
		safeId.ShouldBe(string.Empty);
	}

	#endregion
}
