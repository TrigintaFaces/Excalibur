// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Routing;

/// <summary>
/// Sprint 619 D.1: R1-FIX regression tests for RoutingDecisionAccessor and
/// MessageContext.CachedRoutingDecision fast-path optimization.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RoutingDecisionAccessorShould
{
	private static readonly RoutingDecision TestDecision = RoutingDecision.Success(
		"rabbitmq",
		["billing-service", "inventory-service"],
		["order-route-rule"]);

	#region Fast-Path (MessageContext)

	[Fact]
	public void ReturnNullFromFastPath_WhenNothingSet()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var services = A.Fake<IServiceProvider>();
		var context = new MessageContext(message, services);

		// Act
		var result = RoutingDecisionAccessor.GetRoutingDecisionFast(context);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnCachedDecisionFromFastPath_WhenSetViaAccessor()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var services = A.Fake<IServiceProvider>();
		var context = new MessageContext(message, services);

		// Act
		RoutingDecisionAccessor.SetRoutingDecision(context, TestDecision);
		var result = RoutingDecisionAccessor.GetRoutingDecisionFast(context);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeSameAs(TestDecision);
	}

	[Fact]
	public void SetCachedFieldDirectly_WhenContextIsMessageContext()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var services = A.Fake<IServiceProvider>();
		var context = new MessageContext(message, services);

		// Act
		RoutingDecisionAccessor.SetRoutingDecision(context, TestDecision);

		// Assert -- verify the internal cached field is set
		context.CachedRoutingDecision.ShouldBeSameAs(TestDecision);
	}

	[Fact]
	public void OnlyWriteCachedField_NotFeaturesDictionary_ForMessageContext()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var services = A.Fake<IServiceProvider>();
		var context = new MessageContext(message, services);

		// Act
		RoutingDecisionAccessor.SetRoutingDecision(context, TestDecision);

		// Assert -- fast path returns the decision via CachedRoutingDecision field
		var fromFastPath = RoutingDecisionAccessor.GetRoutingDecisionFast(context);
		fromFastPath.ShouldBeSameAs(TestDecision);

		// The Features dictionary is NOT populated (C.1 dual-write elimination saves ~80B)
		var fromSlowPath = context.GetRoutingDecision();
		fromSlowPath.ShouldBeNull("Features dictionary is not written for MessageContext -- use GetRoutingDecisionFast");
	}

	[Fact]
	public void RoundTripThroughFastPath()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var services = A.Fake<IServiceProvider>();
		var context = new MessageContext(message, services);
		var decision = RoutingDecision.Success("kafka", ["orders"], ["kafka-rule"]);

		// Act
		RoutingDecisionAccessor.SetRoutingDecision(context, decision);
		var retrieved = RoutingDecisionAccessor.GetRoutingDecisionFast(context);

		// Assert
		retrieved.ShouldNotBeNull();
		retrieved!.Transport.ShouldBe("kafka");
		retrieved.Endpoints.ShouldContain("orders");
		retrieved.MatchedRules.ShouldContain("kafka-rule");
		retrieved.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void OverwritePreviousDecisionOnFastPath()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var services = A.Fake<IServiceProvider>();
		var context = new MessageContext(message, services);
		var first = RoutingDecision.Success("rabbitmq", ["svc-a"]);
		var second = RoutingDecision.Success("kafka", ["svc-b"]);

		// Act
		RoutingDecisionAccessor.SetRoutingDecision(context, first);
		RoutingDecisionAccessor.SetRoutingDecision(context, second);
		var result = RoutingDecisionAccessor.GetRoutingDecisionFast(context);

		// Assert
		result.ShouldBeSameAs(second);
		result!.Transport.ShouldBe("kafka");
	}

	#endregion

	#region Slow-Path (Non-MessageContext / IMessageContext)

	[Fact]
	public void FallBackToFeaturesDictionary_WhenContextIsNotMessageContext()
	{
		// Arrange -- use a fake IMessageContext (not MessageContext)
		var context = A.Fake<IMessageContext>();
		var features = new Dictionary<Type, object>();
		A.CallTo(() => context.Features).Returns(features);

		// Act
		RoutingDecisionAccessor.SetRoutingDecision(context, TestDecision);
		var result = RoutingDecisionAccessor.GetRoutingDecisionFast(context);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeSameAs(TestDecision);
	}

	[Fact]
	public void ReturnNullFromSlowPath_WhenNothingSet()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var features = new Dictionary<Type, object>();
		A.CallTo(() => context.Features).Returns(features);

		// Act
		var result = RoutingDecisionAccessor.GetRoutingDecisionFast(context);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void RoundTripThroughSlowPath()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var features = new Dictionary<Type, object>();
		A.CallTo(() => context.Features).Returns(features);
		var decision = RoutingDecision.Success("azure-sb", ["payments"]);

		// Act
		RoutingDecisionAccessor.SetRoutingDecision(context, decision);
		var result = RoutingDecisionAccessor.GetRoutingDecisionFast(context);

		// Assert
		result.ShouldNotBeNull();
		result!.Transport.ShouldBe("azure-sb");
		result.Endpoints.ShouldContain("payments");
	}

	#endregion

	#region CachedRoutingDecision Field Lifecycle

	[Fact]
	public void ClearCachedFieldOnReset()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var services = A.Fake<IServiceProvider>();
		var context = new MessageContext(message, services);

		RoutingDecisionAccessor.SetRoutingDecision(context, TestDecision);
		context.CachedRoutingDecision.ShouldNotBeNull();

		// Act -- Reset clears all state (object pooling scenario)
		context.Reset();

		// Assert
		context.CachedRoutingDecision.ShouldBeNull();
		RoutingDecisionAccessor.GetRoutingDecisionFast(context).ShouldBeNull();
	}

	[Fact]
	public void HandleFailureDecisionOnFastPath()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var services = A.Fake<IServiceProvider>();
		var context = new MessageContext(message, services);
		var failure = RoutingDecision.Failure("No matching routing rules");

		// Act
		RoutingDecisionAccessor.SetRoutingDecision(context, failure);
		var result = RoutingDecisionAccessor.GetRoutingDecisionFast(context);

		// Assert
		result.ShouldNotBeNull();
		result!.IsSuccess.ShouldBeFalse();
		result.FailureReason.ShouldBe("No matching routing rules");
	}

	#endregion

	#region Public Extension Interop

	[Fact]
	public void PublicGetRoutingDecision_ShouldReturnNull_WhenSetViaInternalAccessor_ForMessageContext()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var services = A.Fake<IServiceProvider>();
		var context = new MessageContext(message, services);

		// Set via internal accessor (only writes CachedRoutingDecision, not Features dict)
		RoutingDecisionAccessor.SetRoutingDecision(context, TestDecision);

		// Act -- retrieve via public extension method (reads Features dict, which is NOT populated)
		var publicResult = context.GetRoutingDecision();

		// Assert -- public path returns null because C.1 eliminated the dual-write
		// The internal fast path (GetRoutingDecisionFast) is the correct read path
		publicResult.ShouldBeNull();
		RoutingDecisionAccessor.GetRoutingDecisionFast(context).ShouldBeSameAs(TestDecision);
	}

	[Fact]
	public void PublicSetRoutingFeature_ShouldNotPopulateCachedField()
	{
		// Arrange -- set via public Features dict (bypasses accessor)
		var message = A.Fake<IDispatchMessage>();
		var services = A.Fake<IServiceProvider>();
		var context = new MessageContext(message, services);

		var feature = context.GetOrCreateRoutingFeature();
		feature.RoutingDecision = TestDecision;

		// Act -- fast path reads cached field, which was NOT set
		var fromFastPath = RoutingDecisionAccessor.GetRoutingDecisionFast(context);

		// Assert -- cached field is null (feature dict was set directly, not via accessor)
		// This is expected -- the accessor only populates cache when SetRoutingDecision is used
		fromFastPath.ShouldBeNull("CachedRoutingDecision is only populated by SetRoutingDecision");

		// But slow public path should work
		var fromPublic = context.GetRoutingDecision();
		fromPublic.ShouldBeSameAs(TestDecision);
	}

	#endregion
}
