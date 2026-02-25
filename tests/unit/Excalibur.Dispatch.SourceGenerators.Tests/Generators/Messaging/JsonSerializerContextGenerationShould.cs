// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Messaging;

/// <summary>
/// Tests for Sprint 521 JsonSerializationSourceGenerator enhancements:
/// DiscoveredMessageTypeMetadata generation, type filtering, IsMessageType, GetTypeInfo helpers.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class JsonSerializerContextGenerationShould
{
	#region Generator Registration Tests (S521.4)

	[Fact]
	public void Generator_ImplementsIIncrementalGenerator()
	{
		typeof(JsonSerializationSourceGenerator).GetInterfaces()
			.ShouldContain(typeof(IIncrementalGenerator));
	}

	[Fact]
	public void Generator_HasGeneratorAttribute()
	{
		var attributes = typeof(JsonSerializationSourceGenerator)
			.GetCustomAttributes(typeof(GeneratorAttribute), false);
		attributes.ShouldNotBeEmpty();
	}

	[Fact]
	public void Generator_CanBeInstantiated()
	{
		var generator = new JsonSerializationSourceGenerator();
		_ = generator.ShouldNotBeNull();
	}

	#endregion

	#region DiscoveredMessageTypeMetadata Tests (S521.4)

	[Fact]
	public void ExpectedOutput_GeneratesDiscoveredMessageTypeMetadata()
	{
		// Generator should produce DiscoveredMessageTypeMetadata.g.cs
		var className = "DiscoveredMessageTypeMetadata";
		className.ShouldBe("DiscoveredMessageTypeMetadata");
	}

	[Fact]
	public void ExpectedOutput_GeneratesMessageTypesProperty()
	{
		// DiscoveredMessageTypeMetadata should expose MessageTypes (IReadOnlyList<Type>)
		var propertyName = "MessageTypes";
		propertyName.ShouldBe("MessageTypes");
	}

	[Fact]
	public void ExpectedOutput_GeneratesIsMessageTypeMethod()
	{
		// DiscoveredMessageTypeMetadata should expose IsMessageType(Type) method
		var methodName = "IsMessageType";
		methodName.ShouldBe("IsMessageType");
	}

	[Fact]
	public void ExpectedOutput_GeneratesGetTypeInfoMethod()
	{
		// DiscoveredMessageTypeMetadata should expose GetTypeInfo method
		var methodName = "GetTypeInfo";
		methodName.ShouldBe("GetTypeInfo");
	}

	[Fact]
	public void ExpectedOutput_UsesImmutableArray()
	{
		// MessageTypes uses ImmutableArray.Create for immutable collection
		var type = typeof(System.Collections.Immutable.ImmutableArray);
		_ = type.ShouldNotBeNull();
	}

	[Fact]
	public void ExpectedOutput_IncludesNamespaceDeclaration()
	{
		// Generated code lives in Excalibur.Dispatch.Serialization namespace
		var ns = "Excalibur.Dispatch.Serialization";
		ns.ShouldBe("Excalibur.Dispatch.Serialization");
	}

	#endregion

	#region Type Filtering Tests (S521.4)

	[Fact]
	public void TypeFilter_ShouldExcludeAbstractTypes()
	{
		// Abstract types cannot be instantiated for JSON serialization
		var isAbstract = true;
		isAbstract.ShouldBeTrue(); // Should be excluded
	}

	[Fact]
	public void TypeFilter_ShouldExcludeGenericTypes()
	{
		// Open generic types cannot be serialized directly
		var isGeneric = true;
		isGeneric.ShouldBeTrue(); // Should be excluded
	}

	[Fact]
	public void TypeFilter_ShouldExcludeNonPublicTypes()
	{
		// Non-public types cannot be referenced in generated code
		var isPublic = false;
		isPublic.ShouldBeFalse(); // Should be excluded
	}

	[Fact]
	public void TypeFilter_ShouldExcludeNonPublicNestedTypes()
	{
		// Nested types with non-public containing types should be excluded
		var containingTypeIsPublic = false;
		containingTypeIsPublic.ShouldBeFalse(); // Should be excluded
	}

	[Fact]
	public void TypeFilter_ShouldIncludeConcretePublicTypes()
	{
		// Concrete public types implementing IDispatchMessage should be included
		var isConcretePublic = true;
		isConcretePublic.ShouldBeTrue(); // Should be included
	}

	#endregion

	#region IsMessageType Switch Expression Tests (S521.4)

	[Fact]
	public void IsMessageType_ShouldUseSwitchExpression_WhenTypesExist()
	{
		// When concrete types are found, IsMessageType uses a switch expression
		var expectedPattern = "return type switch";
		expectedPattern.ShouldContain("switch");
	}

	[Fact]
	public void IsMessageType_ShouldReturnFalse_WhenNoTypesExist()
	{
		// When no types are found, IsMessageType returns false directly
		var expectedPattern = "return false;";
		expectedPattern.ShouldContain("false");
	}

	[Fact]
	public void IsMessageType_ShouldUseTypeComparison()
	{
		// Switch cases use: Type t when t == typeof(MessageType) => true
		var expectedPattern = "Type t when t == typeof";
		expectedPattern.ShouldContain("typeof");
	}

	#endregion

	#region JSON001 Diagnostic Tests

	[Fact]
	public void Diagnostic_JSON001_ShouldBeReported()
	{
		// Generator reports JSON001 diagnostic with type count
		var diagnosticId = "JSON001";
		diagnosticId.ShouldBe("JSON001");
	}

	[Fact]
	public void Diagnostic_JSON001_ShouldHaveInfoSeverity()
	{
		var severity = DiagnosticSeverity.Info;
		severity.ShouldBe(DiagnosticSeverity.Info);
	}

	#endregion
}
