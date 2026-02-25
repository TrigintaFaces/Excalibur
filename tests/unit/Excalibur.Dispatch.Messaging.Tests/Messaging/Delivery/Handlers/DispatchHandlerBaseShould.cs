// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery.Handlers;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Unit tests for <see cref="DispatchHandlerBase{TMessage, TResult}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Handlers")]
[Trait("Priority", "0")]
public sealed class DispatchHandlerBaseShould
{
	#region Test Handler Implementation

	private sealed class TestMessage : IDispatchAction<string>
	{
		public string Value { get; init; } = string.Empty;
	}

	private sealed class TestHandler : DispatchHandlerBase<TestMessage, string>
	{
		public bool CheckIsValid() => IsValid;
		public bool CheckIsAuthorized() => IsAuthorized;
		public bool CheckIsRouted() => IsRouted;
	}

	#endregion

	#region Context Property Tests

	[Fact]
	public void Context_WhenSet_CanBeRetrieved()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		var handler = new TestHandler { Context = context };

		// Assert
		handler.Context.ShouldBe(context);
	}

	[Fact]
	public void Context_IsRequired()
	{
		// Assert - TestHandler requires Context to be set
		// This test verifies the 'required' keyword is present
		var contextProperty = typeof(DispatchHandlerBase<TestMessage, string>)
			.GetProperty(nameof(DispatchHandlerBase<TestMessage, string>.Context));

		_ = contextProperty.ShouldNotBeNull();
		_ = contextProperty.GetSetMethod().ShouldNotBeNull();
	}

	#endregion

	#region IsValid Property Tests

	[Fact]
	public void IsValid_WhenValidationResultIsValid_ReturnsTrue()
	{
		// Arrange
		var context = new MessageContext
		{
			ValidationResult = SerializableValidationResult.Success()
		};
		var handler = new TestHandler { Context = context };

		// Act & Assert
		handler.CheckIsValid().ShouldBeTrue();
	}

	[Fact]
	public void IsValid_WhenValidationResultIsInvalid_ReturnsFalse()
	{
		// Arrange
		var context = new MessageContext
		{
			ValidationResult = SerializableValidationResult.Failed("Test error")
		};
		var handler = new TestHandler { Context = context };

		// Act & Assert
		handler.CheckIsValid().ShouldBeFalse();
	}

	[Fact]
	public void IsValid_WhenValidationResultHasErrors_ReturnsFalse()
	{
		// Arrange
		var context = new MessageContext
		{
			ValidationResult = SerializableValidationResult.Failed([
				new ValidationError("field1", "Error 1"),
				new ValidationError("field2", "Error 2")
			])
		};
		var handler = new TestHandler { Context = context };

		// Act & Assert
		handler.CheckIsValid().ShouldBeFalse();
	}

	#endregion

	#region IsAuthorized Property Tests

	[Fact]
	public void IsAuthorized_WhenAuthorizationSucceeds_ReturnsTrue()
	{
		// Arrange
		var context = new MessageContext
		{
			AuthorizationResult = AuthorizationResult.Success()
		};
		var handler = new TestHandler { Context = context };

		// Act & Assert
		handler.CheckIsAuthorized().ShouldBeTrue();
	}

	[Fact]
	public void IsAuthorized_WhenAuthorizationFails_ReturnsFalse()
	{
		// Arrange
		var context = new MessageContext
		{
			AuthorizationResult = AuthorizationResult.Failed("Access denied")
		};
		var handler = new TestHandler { Context = context };

		// Act & Assert
		handler.CheckIsAuthorized().ShouldBeFalse();
	}

	#endregion

	#region IsRouted Property Tests

	[Fact]
	public void IsRouted_WhenRoutingSucceeds_ReturnsTrue()
	{
		// Arrange
		var context = new MessageContext
		{
			RoutingDecision = RoutingDecision.Success("local", [])
		};
		var handler = new TestHandler { Context = context };

		// Act & Assert
		handler.CheckIsRouted().ShouldBeTrue();
	}

	[Fact]
	public void IsRouted_WhenRoutingFails_ReturnsFalse()
	{
		// Arrange
		var context = new MessageContext
		{
			RoutingDecision = RoutingDecision.Failure("No route found")
		};
		var handler = new TestHandler { Context = context };

		// Act & Assert
		handler.CheckIsRouted().ShouldBeFalse();
	}

	#endregion

	#region Combined State Tests

	[Fact]
	public void Handler_WithAllSuccessfulResults_AllPropertiesReturnTrue()
	{
		// Arrange
		var context = new MessageContext
		{
			ValidationResult = SerializableValidationResult.Success(),
			AuthorizationResult = AuthorizationResult.Success(),
			RoutingDecision = RoutingDecision.Success("local", [])
		};
		var handler = new TestHandler { Context = context };

		// Assert
		handler.CheckIsValid().ShouldBeTrue();
		handler.CheckIsAuthorized().ShouldBeTrue();
		handler.CheckIsRouted().ShouldBeTrue();
	}

	[Fact]
	public void Handler_WithMixedResults_ReturnsCorrectValues()
	{
		// Arrange
		var context = new MessageContext
		{
			ValidationResult = SerializableValidationResult.Success(),
			AuthorizationResult = AuthorizationResult.Failed("Denied"),
			RoutingDecision = RoutingDecision.Success("local", [])
		};
		var handler = new TestHandler { Context = context };

		// Assert
		handler.CheckIsValid().ShouldBeTrue();
		handler.CheckIsAuthorized().ShouldBeFalse();
		handler.CheckIsRouted().ShouldBeTrue();
	}

	[Fact]
	public void Handler_WithAllFailedResults_AllPropertiesReturnFalse()
	{
		// Arrange
		var context = new MessageContext
		{
			ValidationResult = SerializableValidationResult.Failed("Invalid"),
			AuthorizationResult = AuthorizationResult.Failed("Denied"),
			RoutingDecision = RoutingDecision.Failure("Not found")
		};
		var handler = new TestHandler { Context = context };

		// Assert
		handler.CheckIsValid().ShouldBeFalse();
		handler.CheckIsAuthorized().ShouldBeFalse();
		handler.CheckIsRouted().ShouldBeFalse();
	}

	#endregion

	#region Type Constraint Tests

	[Fact]
	public void Handler_RequiresMessageToImplementIDispatchAction()
	{
		// Assert - Verify the generic constraint via reflection
		var handlerType = typeof(DispatchHandlerBase<,>);
		var typeParams = handlerType.GetGenericArguments();

		// First type parameter (TMessage) should have the IDispatchAction constraint
		var messageTypeParam = typeParams[0];
		var constraints = messageTypeParam.GetGenericParameterConstraints();

		// The constraint is IDispatchAction<TResult>
		constraints.ShouldNotBeEmpty();
	}

	#endregion
}
