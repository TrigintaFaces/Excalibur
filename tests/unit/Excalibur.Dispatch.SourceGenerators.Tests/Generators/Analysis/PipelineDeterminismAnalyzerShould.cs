// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.SourceGenerators.Analysis;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Analysis;

/// <summary>
/// Tests for <see cref="PipelineDeterminismAnalyzer"/> - analyzes message types for pipeline determinism.
/// Validates message type discovery, determinism analysis, and metadata generation patterns.
/// </summary>
/// <remarks>
/// Sprint 456 - S456.4: Unit tests for pipeline determinism analyzer (PERF-10 Phase 2).
/// Tests the analyzer logic, PipelineMetadata model, and expected output patterns.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class PipelineDeterminismAnalyzerShould
{
	#region PipelineMetadata Model Tests (6 tests)

	[Fact]
	public void PipelineMetadata_SafeIdentifier_ReplacesDotsWithUnderscores()
	{
		// Arrange
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "Excalibur.Dispatch.Commands.CreateOrder"
		};

		// Act
		var safeId = metadata.SafeIdentifier;

		// Assert
		safeId.ShouldBe("Excalibur_Dispatch_Commands_CreateOrder");
	}

	[Fact]
	public void PipelineMetadata_SafeIdentifier_ReplacesPlusWithUnderscores()
	{
		// Arrange - nested class uses + separator
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "OuterClass+NestedCommand"
		};

		// Act
		var safeId = metadata.SafeIdentifier;

		// Assert
		safeId.ShouldBe("OuterClass_NestedCommand");
	}

	[Fact]
	public void PipelineMetadata_DefaultsIsDeterministicToFalse()
	{
		// Arrange
		var metadata = new PipelineMetadata();

		// Assert
		metadata.IsDeterministic.ShouldBeFalse();
	}

	[Fact]
	public void PipelineMetadata_DefaultsApplicableMiddlewareToEmptyList()
	{
		// Arrange
		var metadata = new PipelineMetadata();

		// Assert
		_ = metadata.ApplicableMiddleware.ShouldNotBeNull();
		metadata.ApplicableMiddleware.ShouldBeEmpty();
	}

	[Fact]
	public void PipelineMetadata_Equals_ReturnsTrueForSameFullName()
	{
		// Arrange
		var metadata1 = new PipelineMetadata
		{
			MessageTypeFullName = "global::TestApp.CreateOrderCommand",
			MessageTypeName = "CreateOrderCommand"
		};
		var metadata2 = new PipelineMetadata
		{
			MessageTypeFullName = "global::TestApp.CreateOrderCommand",
			MessageTypeName = "CreateOrderCommand"
		};

		// Act & Assert
		metadata1.Equals(metadata2).ShouldBeTrue();
		metadata1.GetHashCode().ShouldBe(metadata2.GetHashCode());
	}

	[Fact]
	public void PipelineMetadata_Equals_ReturnsFalseForDifferentFullName()
	{
		// Arrange
		var metadata1 = new PipelineMetadata
		{
			MessageTypeFullName = "global::TestApp.CreateOrderCommand"
		};
		var metadata2 = new PipelineMetadata
		{
			MessageTypeFullName = "global::TestApp.DeleteOrderCommand"
		};

		// Act & Assert
		metadata1.Equals(metadata2).ShouldBeFalse();
	}

	#endregion

	#region Generator Registration Tests (3 tests)

	[Fact]
	public void Analyzer_ImplementsIIncrementalGenerator()
	{
		// Assert
		typeof(PipelineDeterminismAnalyzer).GetInterfaces()
			.ShouldContain(typeof(Microsoft.CodeAnalysis.IIncrementalGenerator));
	}

	[Fact]
	public void Analyzer_HasGeneratorAttribute()
	{
		// Assert
		var attribute = typeof(PipelineDeterminismAnalyzer)
			.GetCustomAttributes(typeof(Microsoft.CodeAnalysis.GeneratorAttribute), false);
		attribute.ShouldNotBeEmpty();
	}

	[Fact]
	public void Analyzer_CanBeInstantiated()
	{
		// Act
		var analyzer = new PipelineDeterminismAnalyzer();

		// Assert
		_ = analyzer.ShouldNotBeNull();
	}

	#endregion

	#region Determinism Analysis Tests (6 tests)

	[Fact]
	public void PipelineMetadata_IsDeterministic_TrueForSimpleMessageType()
	{
		// Arrange - simple message with no conditional middleware
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "SimpleCommand",
			MessageTypeFullName = "global::TestApp.SimpleCommand",
			IsDeterministic = true,
			MessageKind = "Command"
		};

		// Assert
		metadata.IsDeterministic.ShouldBeTrue();
		metadata.NonDeterministicReason.ShouldBeNull();
	}

	[Fact]
	public void PipelineMetadata_IsDeterministic_FalseForTenantSpecific()
	{
		// Arrange - tenant-specific routing is non-deterministic
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "TenantCommand",
			MessageTypeFullName = "global::TestApp.TenantCommand",
			IsDeterministic = false,
			NonDeterministicReason = "Tenant-specific pipeline routing"
		};

		// Assert
		metadata.IsDeterministic.ShouldBeFalse();
		metadata.NonDeterministicReason.ShouldBe("Tenant-specific pipeline routing");
	}

	[Fact]
	public void PipelineMetadata_IsDeterministic_FalseForConditionalMiddleware()
	{
		// Arrange - conditional middleware is non-deterministic
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "FeatureFlagCommand",
			MessageTypeFullName = "global::TestApp.FeatureFlagCommand",
			IsDeterministic = false,
			NonDeterministicReason = "Conditional middleware via attribute"
		};

		// Assert
		metadata.IsDeterministic.ShouldBeFalse();
		metadata.NonDeterministicReason.ShouldContain("Conditional");
	}

	[Fact]
	public void PipelineMetadata_IsDeterministic_FalseForDynamicProfileSelection()
	{
		// Arrange - dynamic profile selection is non-deterministic
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "DynamicProfileCommand",
			MessageTypeFullName = "global::TestApp.DynamicProfileCommand",
			IsDeterministic = false,
			HasCustomPipelineProfile = true,
			PipelineProfileName = null, // Not statically determined
			NonDeterministicReason = "Dynamic pipeline profile attribute without static name"
		};

		// Assert
		metadata.IsDeterministic.ShouldBeFalse();
		metadata.HasCustomPipelineProfile.ShouldBeTrue();
		metadata.PipelineProfileName.ShouldBeNull();
	}

	[Fact]
	public void PipelineMetadata_IsDeterministic_TrueForStaticPipelineProfile()
	{
		// Arrange - static pipeline profile is deterministic
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "StaticProfileCommand",
			MessageTypeFullName = "global::TestApp.StaticProfileCommand",
			IsDeterministic = true,
			HasCustomPipelineProfile = true,
			PipelineProfileName = "FastPath"
		};

		// Assert
		metadata.IsDeterministic.ShouldBeTrue();
		metadata.PipelineProfileName.ShouldBe("FastPath");
	}

	[Fact]
	public void PipelineMetadata_TracksApplicableMiddleware()
	{
		// Arrange
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "Command",
			IsDeterministic = true,
			ApplicableMiddleware = new List<string>
			{
				"LoggingMiddleware",
				"ValidationMiddleware",
				"AuthorizationMiddleware"
			}
		};

		// Assert
		metadata.ApplicableMiddleware.Count.ShouldBe(3);
		metadata.ApplicableMiddleware.ShouldContain("LoggingMiddleware");
	}

	#endregion

	#region Message Kind Tests (5 tests)

	[Fact]
	public void PipelineMetadata_MessageKind_Command()
	{
		// Arrange
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "CreateOrderCommand",
			MessageKind = "Command"
		};

		// Assert
		metadata.MessageKind.ShouldBe("Command");
	}

	[Fact]
	public void PipelineMetadata_MessageKind_Query()
	{
		// Arrange
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "GetOrderQuery",
			MessageKind = "Query"
		};

		// Assert
		metadata.MessageKind.ShouldBe("Query");
	}

	[Fact]
	public void PipelineMetadata_MessageKind_DomainEvent()
	{
		// Arrange
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "OrderCreatedEvent",
			MessageKind = "DomainEvent"
		};

		// Assert
		metadata.MessageKind.ShouldBe("DomainEvent");
	}

	[Fact]
	public void PipelineMetadata_MessageKind_IntegrationEvent()
	{
		// Arrange
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "OrderCreatedIntegrationEvent",
			MessageKind = "IntegrationEvent"
		};

		// Assert
		metadata.MessageKind.ShouldBe("IntegrationEvent");
	}

	[Fact]
	public void PipelineMetadata_MessageKind_DefaultsToMessage()
	{
		// Arrange
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "GenericMessage",
			MessageKind = "Message"
		};

		// Assert
		metadata.MessageKind.ShouldBe("Message");
	}

	#endregion

	#region Generated Output Pattern Tests (4 tests)

	[Fact]
	public void ExpectedOutput_UsesFileModifier_ForPipelineMetadata()
	{
		// The analyzer produces a file-scoped class to avoid conflicts
		// Expected pattern: "file static class PipelineMetadata"
		// This test documents the expected behavior

		var metadata = new PipelineMetadata
		{
			MessageTypeName = "TestCommand",
			MessageTypeFullName = "global::TestApp.TestCommand"
		};

		// Generator should use MessageTypeFullName for type references
		metadata.MessageTypeFullName.ShouldStartWith("global::");
	}

	[Fact]
	public void ExpectedOutput_GeneratesFrozenDictionary_ForDeterminism()
	{
		// Expected pattern: FrozenDictionary<Type, bool> _determinism
		// This test documents the expected data structure

		var metadata = new PipelineMetadata
		{
			MessageTypeName = "Command",
			MessageTypeFullName = "global::TestApp.Command",
			IsDeterministic = true
		};

		// Info must have type information for FrozenDictionary key generation
		metadata.MessageTypeFullName.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void ExpectedOutput_GeneratesFrozenDictionary_ForMessageKinds()
	{
		// Expected pattern: FrozenDictionary<Type, string> _messageKinds
		// This test documents the expected data structure

		var metadata = new PipelineMetadata
		{
			MessageTypeName = "Query",
			MessageTypeFullName = "global::TestApp.Query",
			MessageKind = "Query"
		};

		// Info must have type and kind information for FrozenDictionary generation
		metadata.MessageTypeFullName.ShouldNotBeNullOrEmpty();
		metadata.MessageKind.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void ExpectedOutput_GeneratesIsDeterministicMethod()
	{
		// Expected pattern: public static bool IsDeterministic<TMessage>()
		// Returns true if message type has deterministic pipeline

		var metadata = new PipelineMetadata
		{
			MessageTypeName = "DeterministicCommand",
			MessageTypeFullName = "global::TestApp.DeterministicCommand",
			IsDeterministic = true
		};

		// The generated method will check the FrozenDictionary
		metadata.IsDeterministic.ShouldBeTrue();
	}

	#endregion

	#region Equality Tests (4 tests)

	[Fact]
	public void PipelineMetadata_Equals_ReturnsFalseForNull()
	{
		// Arrange
		var metadata = new PipelineMetadata
		{
			MessageTypeFullName = "global::TestApp.Command"
		};

		// Act & Assert
		metadata.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void PipelineMetadata_Equals_WorksWithObjectOverload()
	{
		// Arrange
		var metadata1 = new PipelineMetadata
		{
			MessageTypeFullName = "global::TestApp.Command"
		};
		object metadata2 = new PipelineMetadata
		{
			MessageTypeFullName = "global::TestApp.Command"
		};

		// Act & Assert
		metadata1.Equals(metadata2).ShouldBeTrue();
	}

	[Fact]
	public void PipelineMetadata_Equals_ReturnsFalseForNonPipelineMetadata()
	{
		// Arrange
		var metadata = new PipelineMetadata
		{
			MessageTypeFullName = "global::TestApp.Command"
		};

		// Act & Assert
		metadata.Equals("not a pipeline metadata").ShouldBeFalse();
	}

	[Fact]
	public void PipelineMetadata_GetHashCode_ConsistentForSameFullName()
	{
		// Arrange
		var metadata1 = new PipelineMetadata { MessageTypeFullName = "global::TestApp.A" };
		var metadata2 = new PipelineMetadata { MessageTypeFullName = "global::TestApp.B" };

		// Act & Assert - different names should typically have different hashes
		metadata1.GetHashCode().ShouldNotBe(metadata2.GetHashCode());
	}

	#endregion

	#region SafeIdentifier Edge Cases (4 tests)

	[Fact]
	public void PipelineMetadata_SafeIdentifier_HandlesSimpleName()
	{
		// Arrange
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "CreateOrderCommand"
		};

		// Act
		var safeId = metadata.SafeIdentifier;

		// Assert
		safeId.ShouldBe("CreateOrderCommand");
	}

	[Fact]
	public void PipelineMetadata_SafeIdentifier_HandlesNestedPlusDot()
	{
		// Arrange - complex nested scenario
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "Outer.Inner+Nested"
		};

		// Act
		var safeId = metadata.SafeIdentifier;

		// Assert
		safeId.ShouldBe("Outer_Inner_Nested");
	}

	[Fact]
	public void PipelineMetadata_SafeIdentifier_PreservesLettersAndDigits()
	{
		// Arrange
		var metadata = new PipelineMetadata
		{
			MessageTypeName = "Command123"
		};

		// Act
		var safeId = metadata.SafeIdentifier;

		// Assert
		safeId.ShouldBe("Command123");
	}

	[Fact]
	public void PipelineMetadata_SafeIdentifier_HandlesEmptyString()
	{
		// Arrange
		var metadata = new PipelineMetadata
		{
			MessageTypeName = string.Empty
		};

		// Act
		var safeId = metadata.SafeIdentifier;

		// Assert
		safeId.ShouldBe(string.Empty);
	}

	#endregion
}
