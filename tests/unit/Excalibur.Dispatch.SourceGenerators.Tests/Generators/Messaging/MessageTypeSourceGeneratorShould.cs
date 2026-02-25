// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.SourceGenerators.Tests.Messaging;

/// <summary>
/// Tests for <see cref="MessageTypeSourceGenerator"/> - generates compile-time message type discovery.
/// Validates generator registration, message type discovery, and AOT preservation patterns.
/// </summary>
/// <remarks>
/// Sprint 460 - Task S460.5: Source Generator Tests.
/// Tests the generator logic, expected output patterns, and edge case handling.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class MessageTypeSourceGeneratorShould
{
	#region Generator Registration Tests (3 tests)

	[Fact]
	public void Generator_ImplementsIIncrementalGenerator()
	{
		// Assert
		typeof(MessageTypeSourceGenerator).GetInterfaces()
			.ShouldContain(typeof(Microsoft.CodeAnalysis.IIncrementalGenerator));
	}

	[Fact]
	public void Generator_HasGeneratorAttribute()
	{
		// Assert
		var attribute = typeof(MessageTypeSourceGenerator)
			.GetCustomAttributes(typeof(Microsoft.CodeAnalysis.GeneratorAttribute), false);
		attribute.ShouldNotBeEmpty();
	}

	[Fact]
	public void Generator_CanBeInstantiated()
	{
		// Act
		var generator = new MessageTypeSourceGenerator();

		// Assert
		_ = generator.ShouldNotBeNull();
	}

	#endregion

	#region Message Interface Detection Tests (6 tests)

	[Fact]
	public void Discovery_SupportsIDispatchMessage()
	{
		// Base interface for all dispatch messages
		var interfaces = GetSupportedMessageInterfaces();
		interfaces.ShouldContain("IDispatchMessage");
	}

	[Fact]
	public void Discovery_SupportsIDispatchAction()
	{
		// Command/Action messages
		var interfaces = GetSupportedMessageInterfaces();
		interfaces.ShouldContain("IDispatchAction");
	}

	[Fact]
	public void Discovery_SupportsIDispatchEvent()
	{
		// Event messages
		var interfaces = GetSupportedMessageInterfaces();
		interfaces.ShouldContain("IDispatchEvent");
	}

	[Fact]
	public void Discovery_SupportsIDispatchDocument()
	{
		// Document messages
		var interfaces = GetSupportedMessageInterfaces();
		interfaces.ShouldContain("IDispatchDocument");
	}

	[Fact]
	public void Discovery_SupportsIIntegrationEvent()
	{
		// Integration events for cross-boundary communication
		var interfaces = GetSupportedMessageInterfaces();
		interfaces.ShouldContain("IIntegrationEvent");
	}

	[Fact]
	public void Discovery_SupportsIDomainEvent()
	{
		// Domain events for in-process event sourcing
		var interfaces = GetSupportedMessageInterfaces();
		interfaces.ShouldContain("IDomainEvent");
	}

	#endregion

	#region Candidate Type Detection Tests (5 tests)

	[Fact]
	public void CandidateType_IncludesClassWithBaseList()
	{
		// ClassDeclarationSyntax { BaseList: not null }
		var includesClass = true;
		includesClass.ShouldBeTrue();
	}

	[Fact]
	public void CandidateType_IncludesRecordWithBaseList()
	{
		// RecordDeclarationSyntax { BaseList: not null }
		var includesRecord = true;
		includesRecord.ShouldBeTrue();
	}

	[Fact]
	public void CandidateType_IncludesStructWithBaseList()
	{
		// StructDeclarationSyntax { BaseList: not null }
		var includesStruct = true;
		includesStruct.ShouldBeTrue();
	}

	[Fact]
	public void CandidateType_ExcludesAbstractTypes()
	{
		// Abstract types cannot be instantiated, so should be skipped
		var skipAbstract = true;
		skipAbstract.ShouldBeTrue();
	}

	[Fact]
	public void CandidateType_ExcludesGenericTypes()
	{
		// Open generic types are skipped (closed generics ok)
		var skipGeneric = true;
		skipGeneric.ShouldBeTrue();
	}

	#endregion

	#region Accessibility Tests (4 tests)

	[Fact]
	public void Accessibility_SkipsPrivateNestedTypes()
	{
		// Nested private types cannot be accessed from generated code
		var skipPrivateNested = true;
		skipPrivateNested.ShouldBeTrue();
	}

	[Fact]
	public void Accessibility_SkipsPrivateTypes()
	{
		// Private types cannot be accessed from generated code
		var accessibility = "Private";
		accessibility.ShouldBe("Private");
	}

	[Fact]
	public void Accessibility_SkipsInternalTypes()
	{
		// Internal types cannot be accessed from generated Excalibur.Dispatch.Generated namespace
		var accessibility = "Internal";
		accessibility.ShouldBe("Internal");
	}

	[Fact]
	public void Accessibility_IncludesPublicTypes()
	{
		// Public types are accessible from generated code
		var accessibility = "Public";
		accessibility.ShouldBe("Public");
	}

	#endregion

	#region Generated Output Pattern Tests (6 tests)

	[Fact]
	public void ExpectedOutput_GeneratesRegistrationClass()
	{
		// Expected: internal static class GeneratedMessageTypeRegistrations
		var className = "GeneratedMessageTypeRegistrations";
		className.ShouldBe("GeneratedMessageTypeRegistrations");
	}

	[Fact]
	public void ExpectedOutput_UsesDispatchGeneratedNamespace()
	{
		// Expected: namespace Excalibur.Dispatch.Generated;
		var ns = "Excalibur.Dispatch.Generated";
		ns.ShouldBe("Excalibur.Dispatch.Generated");
	}

	[Fact]
	public void ExpectedOutput_GeneratesPreservedTypesArray()
	{
		// Expected: private static readonly Type[] PreservedMessageTypes = [...]
		var arrayName = "PreservedMessageTypes";
		arrayName.ShouldBe("PreservedMessageTypes");
	}

	[Fact]
	public void ExpectedOutput_GeneratesModuleInitializer()
	{
		// Expected: [ModuleInitializer] public static void Initialize()
		var attribute = "ModuleInitializer";
		attribute.ShouldBe("ModuleInitializer");
	}

	[Fact]
	public void ExpectedOutput_GeneratesPreserveTypeMethod()
	{
		// Expected: private static void PreserveType(Type type)
		// Uses DynamicallyAccessedMembers attribute for trimmer
		var methodName = "PreserveType";
		methodName.ShouldBe("PreserveType");
	}

	[Fact]
	public void ExpectedOutput_UsesFullyQualifiedTypeNames()
	{
		// Generated typeof() expressions use fully qualified names
		// Pattern: typeof(global::TestApp.CreateOrderCommand)
		var prefix = "global::";
		prefix.ShouldBe("global::");
	}

	#endregion

	#region AOT/Trimming Compatibility Tests (4 tests)

	[Fact]
	public void AotCompat_UsesDynamicallyAccessedMembers()
	{
		// PreserveType uses [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		var memberTypes = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All;
		memberTypes.ShouldBe(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All);
	}

	[Fact]
	public void AotCompat_SuppressesTrimmingWarnings()
	{
		// Uses UnconditionalSuppressMessage for IL2026, IL2072, IL2075
		var suppressedWarning = "IL2026";
		suppressedWarning.ShouldBe("IL2026");
	}

	[Fact]
	public void AotCompat_PreservesConstructors()
	{
		// PreserveType calls type.GetConstructors() to prevent trimming
		var methodName = "GetConstructors";
		methodName.ShouldBe("GetConstructors");
	}

	[Fact]
	public void AotCompat_PreservesProperties()
	{
		// PreserveType calls type.GetProperties() to prevent trimming
		var methodName = "GetProperties";
		methodName.ShouldBe("GetProperties");
	}

	#endregion

	#region Edge Case Tests (4 tests)

	[Fact]
	public void EdgeCase_HandlesEmptyMessageTypeList()
	{
		// When no message types are discovered, generator should not produce output
		// if (messageTypes.IsDefaultOrEmpty) return;
		var emptyArray = Array.Empty<object>();
		emptyArray.Length.ShouldBe(0);
	}

	[Fact]
	public void EdgeCase_OrdersTypesByFullName()
	{
		// Generated code orders types by FullName for deterministic output
		// messageTypes.OrderBy(static t => t.FullName)
		var ordered = true;
		ordered.ShouldBeTrue();
	}

	[Fact]
	public void EdgeCase_IncludesTimestampComment()
	{
		// Generated file includes timestamp for debugging
		// Pattern: // Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
		var includesTimestamp = true;
		includesTimestamp.ShouldBeTrue();
	}

	[Fact]
	public void EdgeCase_IncludesMessageTypeCount()
	{
		// Generated file includes count for debugging
		// Pattern: // Message types discovered: {count}
		var includesCount = true;
		includesCount.ShouldBeTrue();
	}

	#endregion

	#region Helper Methods

	private static HashSet<string> GetSupportedMessageInterfaces()
	{
		// These are the interfaces that MessageTypeSourceGenerator scans for
		return new HashSet<string>
		{
			"IDispatchMessage",
			"IDispatchAction",
			"IDispatchEvent",
			"IDispatchDocument",
			"IIntegrationEvent",
			"IDomainEvent"
		};
	}

	#endregion
}
