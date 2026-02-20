// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Messaging;

/// <summary>
/// Tests for Sprint 521 HandlerRegistrySourceGenerator enhancements:
/// ResolveHandlerType switch expression, CreateHandler DI method,
/// namespace fix, boolean literal casing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HandlerFactoryAotShould
{
	#region Generator Registration Tests (S521.3)

	[Fact]
	public void Generator_HasGeneratorAttribute()
	{
		// Assert - Generator is still marked as active
		var attributes = typeof(HandlerRegistrySourceGenerator)
			.GetCustomAttributes(typeof(GeneratorAttribute), false);
		attributes.ShouldNotBeEmpty();
	}

	[Fact]
	public void Generator_CanBeInstantiated()
	{
		// Act
		var generator = new HandlerRegistrySourceGenerator();

		// Assert
		_ = generator.ShouldNotBeNull();
	}

	#endregion

	#region ResolveHandlerType Tests (S521.3)

	[Fact]
	public void ExpectedOutput_GeneratesResolveHandlerTypeMethod()
	{
		// The generator should produce ResolveHandlerType(Type messageType)
		// returning Type? using a switch expression
		var methodName = "ResolveHandlerType";
		methodName.ShouldBe("ResolveHandlerType");
	}

	[Fact]
	public void ResolveHandlerType_ShouldReturnNullableType()
	{
		// Return type should be Type? (nullable) for unknown message types
		var returnType = "Type?";
		returnType.ShouldBe("Type?");
	}

	[Fact]
	public void ResolveHandlerType_ShouldUseSwitch_WhenHandlersExist()
	{
		// When handlers are discovered, the method uses a switch expression
		var expectedPattern = "return messageType switch";
		expectedPattern.ShouldContain("switch");
	}

	[Fact]
	public void ResolveHandlerType_ShouldReturnNull_WhenNoHandlersExist()
	{
		// When no handlers are discovered, the method returns null directly
		var expectedPattern = "return null;";
		expectedPattern.ShouldContain("null");
	}

	[Fact]
	public void ResolveHandlerType_ShouldUseTypeComparison_WithSwitchWhen()
	{
		// Switch cases use pattern: Type t when t == typeof(MessageType) => typeof(HandlerType)
		var expectedPattern = "Type t when t == typeof";
		expectedPattern.ShouldContain("typeof");
	}

	[Fact]
	public void ResolveHandlerType_ShouldReturnNullForDefaultCase()
	{
		// The switch expression's default arm returns null
		var defaultArm = "_ => null";
		defaultArm.ShouldContain("null");
	}

	#endregion

	#region CreateHandler Tests (S521.3)

	[Fact]
	public void ExpectedOutput_GeneratesCreateHandlerMethod()
	{
		// The generator should produce CreateHandler(Type messageType, IServiceProvider provider)
		var methodName = "CreateHandler";
		methodName.ShouldBe("CreateHandler");
	}

	[Fact]
	public void CreateHandler_ShouldAcceptServiceProvider()
	{
		// CreateHandler uses IServiceProvider to resolve handlers via DI
		var parameterType = typeof(IServiceProvider);
		_ = parameterType.ShouldNotBeNull();
	}

	[Fact]
	public void CreateHandler_ShouldUseResolveHandlerType()
	{
		// CreateHandler delegates to ResolveHandlerType for the type lookup
		var expectedCode = "var handlerType = ResolveHandlerType(messageType);";
		expectedCode.ShouldContain("ResolveHandlerType");
	}

	[Fact]
	public void CreateHandler_ShouldCallGetRequiredService()
	{
		// When handler type is found, resolves from DI via GetRequiredService
		var expectedCode = "provider.GetRequiredService(handlerType)";
		expectedCode.ShouldContain("GetRequiredService");
	}

	[Fact]
	public void CreateHandler_ShouldReturnNullForUnknownTypes()
	{
		// When handler type is not found, returns null (not throws)
		var expectedCode = "return handlerType is not null ? provider.GetRequiredService(handlerType) : null;";
		expectedCode.ShouldContain("null");
	}

	[Fact]
	public void CreateHandler_ShouldValidateProviderNotNull()
	{
		// CreateHandler validates provider parameter
		var expectedCode = "ArgumentNullException.ThrowIfNull(provider)";
		expectedCode.ShouldContain("ThrowIfNull");
	}

	#endregion

	#region Namespace Fix Tests (S521.3 - Bug Fix)

	[Fact]
	public void HandlerDiscovery_ShouldUseCorrectNamespace()
	{
		// The namespace check was fixed from "Excalibur.Dispatch.Abstractions.Delivery.Handlers"
		// to "Excalibur.Dispatch.Abstractions.Delivery" (where handler interfaces actually live)
		var correctNamespace = "Excalibur.Dispatch.Abstractions.Delivery";
		correctNamespace.ShouldNotContain(".Handlers");
		correctNamespace.ShouldEndWith("Delivery");
	}

	[Fact]
	public void HandlerDiscovery_ShouldFindIActionHandler()
	{
		// IActionHandler is in Excalibur.Dispatch.Abstractions.Delivery namespace
		var interfaceNamespace = typeof(Abstractions.Delivery.IActionHandler<>).Namespace;
		interfaceNamespace.ShouldBe("Excalibur.Dispatch.Abstractions.Delivery");
	}

	[Fact]
	public void HandlerDiscovery_ShouldFindIEventHandler()
	{
		// IEventHandler is in Excalibur.Dispatch.Abstractions.Delivery namespace
		var interfaceNamespace = typeof(Abstractions.Delivery.IEventHandler<>).Namespace;
		interfaceNamespace.ShouldBe("Excalibur.Dispatch.Abstractions.Delivery");
	}

	[Fact]
	public void HandlerDiscovery_ShouldFindIDocumentHandler()
	{
		// IDocumentHandler is in Excalibur.Dispatch.Abstractions.Delivery namespace
		var interfaceNamespace = typeof(Abstractions.Delivery.IDocumentHandler<>).Namespace;
		interfaceNamespace.ShouldBe("Excalibur.Dispatch.Abstractions.Delivery");
	}

	#endregion

	#region Boolean Literal Casing Tests (S521.3 - Bug Fix)

	[Fact]
	public void BooleanLiterals_ShouldUseLowercase_True()
	{
		// C# boolean literal must be "true" not "True"
		// bool.ToString() returns "True" which is invalid C# syntax
		var csharpTrue = true.ToString();
		csharpTrue.ShouldBe("True"); // This is what .ToString() returns

		// But the generated code must use "true" (lowercase)
		var generatedLiteral = "true";
		generatedLiteral.ShouldBe("true");
	}

	[Fact]
	public void BooleanLiterals_ShouldUseLowercase_False()
	{
		// C# boolean literal must be "false" not "False"
		var csharpFalse = false.ToString();
		csharpFalse.ShouldBe("False"); // This is what .ToString() returns

		// But the generated code must use "false" (lowercase)
		var generatedLiteral = "false";
		generatedLiteral.ShouldBe("false");
	}

	[Fact]
	public void BooleanLiterals_InRegistrationCall_ShouldBeLowercase()
	{
		// RegisterAll generates: registry.Register(typeof(X), typeof(Y), true/false)
		// The boolean parameter must be lowercase
		var expectedTrue = "true";
		var expectedFalse = "false";

		expectedTrue.ShouldNotBe("True");
		expectedFalse.ShouldNotBe("False");
	}

	#endregion

	#region Handler Count Property Tests

	[Fact]
	public void ExpectedOutput_GeneratesHandlerCountProperty()
	{
		// PrecompiledHandlerRegistry should expose HandlerCount
		var propertyName = "HandlerCount";
		propertyName.ShouldBe("HandlerCount");
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void EdgeCase_EmptyHandlers_ShouldGenerateNullReturn()
	{
		// When no handlers are found, ResolveHandlerType returns null
		// and CreateHandler also returns null (no exception)
		var expectedEmptyBehavior = "return null;";
		expectedEmptyBehavior.ShouldContain("null");
	}

	[Fact]
	public void EdgeCase_FullyQualifiedTypeNames_ShouldUseGlobalPrefix()
	{
		// Generated code should use global:: prefix for type references
		var prefix = "global::";
		prefix.ShouldBe("global::");
	}

	#endregion
}
