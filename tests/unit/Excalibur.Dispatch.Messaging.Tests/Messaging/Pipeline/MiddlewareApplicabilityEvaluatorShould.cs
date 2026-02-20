// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Pipeline;

/// <summary>
/// Unit tests for <see cref="MiddlewareApplicabilityEvaluator" /> covering message kind applicability,
/// exclusion logic, feature requirements, filtering, and the three-phase lazy freeze pattern.
/// </summary>
/// <remarks>
/// Sprint 461 - S461.1: Coverage tests for 0% coverage classes.
/// Target: Increase MiddlewareApplicabilityEvaluator coverage from 0% to 80%+.
/// </remarks>
[Collection("HandlerInvokerRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class MiddlewareApplicabilityEvaluatorShould : IDisposable
{
	private readonly ILogger<MiddlewareApplicabilityEvaluator> _logger;
	private readonly IOptions<MiddlewareApplicabilityOptions> _options;

	public MiddlewareApplicabilityEvaluatorShould()
	{
		_logger = A.Fake<ILogger<MiddlewareApplicabilityEvaluator>>();
		_options = Microsoft.Extensions.Options.Options.Create(new MiddlewareApplicabilityOptions());

		// Clear static cache before each test
		MiddlewareApplicabilityEvaluator.ClearCache();
	}

	public void Dispose()
	{
		// Clear static cache after each test
		MiddlewareApplicabilityEvaluator.ClearCache();
	}

	private MiddlewareApplicabilityEvaluator CreateEvaluator(MiddlewareApplicabilityOptions? customOptions = null)
	{
		var options = customOptions != null
			? Microsoft.Extensions.Options.Options.Create(customOptions)
			: _options;
		return new MiddlewareApplicabilityEvaluator(options, _logger);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_Should_Throw_When_Options_Is_Null()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MiddlewareApplicabilityEvaluator(null!, _logger));
	}

	[Fact]
	public void Constructor_Should_Throw_When_Logger_Is_Null()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MiddlewareApplicabilityEvaluator(_options, null!));
	}

	[Fact]
	public void Constructor_Should_Accept_Valid_Parameters()
	{
		// Act
		var evaluator = CreateEvaluator();

		// Assert
		_ = evaluator.ShouldNotBeNull();
	}

	#endregion

	#region IsApplicable (Type, MessageKind) Tests

	[Fact]
	public void IsApplicable_ByType_Should_Throw_When_MiddlewareType_Is_Null()
	{
		// Arrange
		var evaluator = CreateEvaluator();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			evaluator.IsApplicable((Type)null!, MessageKinds.Action));
	}

	[Fact]
	public void IsApplicable_ByType_Should_Return_True_For_Middleware_Without_Attribute()
	{
		// Arrange - Middleware without AppliesToAttribute defaults to All
		var evaluator = CreateEvaluator();

		// Act
		var result = evaluator.IsApplicable(typeof(NoAttributeMiddleware), MessageKinds.Action);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_ByType_Should_Return_True_For_Matching_MessageKind()
	{
		// Arrange
		var evaluator = CreateEvaluator();

		// Act
		var result = evaluator.IsApplicable(typeof(ActionOnlyMiddleware), MessageKinds.Action);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_ByType_Should_Return_False_For_NonMatching_MessageKind()
	{
		// Arrange
		var evaluator = CreateEvaluator();

		// Act
		var result = evaluator.IsApplicable(typeof(ActionOnlyMiddleware), MessageKinds.Event);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsApplicable_ByType_Should_Return_False_When_MessageKind_Is_Excluded()
	{
		// Arrange - Middleware that applies to All but excludes Events
		var evaluator = CreateEvaluator();

		// Act
		var result = evaluator.IsApplicable(typeof(ExcludeEventsMiddleware), MessageKinds.Event);

		// Assert - Exclusion overrides inclusion
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsApplicable_ByType_Should_Return_True_When_MessageKind_Is_Not_Excluded()
	{
		// Arrange - Middleware that applies to All but excludes Events
		var evaluator = CreateEvaluator();

		// Act
		var result = evaluator.IsApplicable(typeof(ExcludeEventsMiddleware), MessageKinds.Action);

		// Assert
		result.ShouldBeTrue();
	}

	[Theory]
	[InlineData(MessageKinds.Action)]
	[InlineData(MessageKinds.Event)]
	[InlineData(MessageKinds.Document)]
	public void IsApplicable_ByType_Should_Return_True_For_All_MessageKinds_Middleware(MessageKinds messageKind)
	{
		// Arrange
		var evaluator = CreateEvaluator();

		// Act
		var result = evaluator.IsApplicable(typeof(AllKindsMiddleware), messageKind);

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region IsApplicable (Type, MessageKind, EnabledFeatures) Tests

	[Fact]
	public void IsApplicable_WithFeatures_Should_Throw_When_MiddlewareType_Is_Null()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		var features = new HashSet<DispatchFeatures> { DispatchFeatures.Tracing };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			evaluator.IsApplicable(null!, MessageKinds.Action, features));
	}

	[Fact]
	public void IsApplicable_WithFeatures_Should_Throw_When_EnabledFeatures_Is_Null()
	{
		// Arrange
		var evaluator = CreateEvaluator();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			evaluator.IsApplicable(typeof(NoAttributeMiddleware), MessageKinds.Action, null!));
	}

	[Fact]
	public void IsApplicable_WithFeatures_Should_Return_False_When_Required_Feature_Missing()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		var features = new HashSet<DispatchFeatures>(); // No features enabled

		// Act
		var result = evaluator.IsApplicable(typeof(RequiresTracingMiddleware), MessageKinds.Action, features);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsApplicable_WithFeatures_Should_Return_True_When_Required_Feature_Present()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		var features = new HashSet<DispatchFeatures> { DispatchFeatures.Tracing };

		// Act
		var result = evaluator.IsApplicable(typeof(RequiresTracingMiddleware), MessageKinds.Action, features);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_WithFeatures_Should_Return_False_When_Any_Required_Feature_Missing()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		var features = new HashSet<DispatchFeatures> { DispatchFeatures.Tracing }; // Missing Metrics

		// Act
		var result = evaluator.IsApplicable(typeof(RequiresMultipleFeaturesMiddleware), MessageKinds.Action, features);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsApplicable_WithFeatures_Should_Return_True_When_All_Required_Features_Present()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		var features = new HashSet<DispatchFeatures> { DispatchFeatures.Tracing, DispatchFeatures.Metrics };

		// Act
		var result = evaluator.IsApplicable(typeof(RequiresMultipleFeaturesMiddleware), MessageKinds.Action, features);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_WithFeatures_Should_Return_False_When_MessageKind_NotApplicable()
	{
		// Arrange - Even if features are present, message kind must match
		var evaluator = CreateEvaluator();
		var features = new HashSet<DispatchFeatures> { DispatchFeatures.Tracing };

		// Act
		var result = evaluator.IsApplicable(typeof(ActionOnlyMiddleware), MessageKinds.Event, features);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region IsApplicable (IDispatchMiddleware, MessageKind) Tests

	[Fact]
	public void IsApplicable_ByInstance_Should_Throw_When_Middleware_Is_Null()
	{
		// Arrange
		var evaluator = CreateEvaluator();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			evaluator.IsApplicable((IDispatchMiddleware)null!, MessageKinds.Action));
	}

	[Fact]
	public void IsApplicable_ByInstance_Should_Use_Interface_Property_When_No_Attribute()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.ApplicableMessageKinds).Returns(MessageKinds.Action);

		// Act
		var actionResult = evaluator.IsApplicable(middleware, MessageKinds.Action);
		var eventResult = evaluator.IsApplicable(middleware, MessageKinds.Event);

		// Assert
		actionResult.ShouldBeTrue();
		eventResult.ShouldBeFalse();
	}

	[Fact]
	public void IsApplicable_ByInstance_Should_Prefer_Attribute_Over_Interface_Property()
	{
		// Arrange - Attribute says Event, interface says Action
		var evaluator = CreateEvaluator();
		var middleware = new EventOnlyMiddlewareInstance();

		// Act
		var eventResult = evaluator.IsApplicable(middleware, MessageKinds.Event);
		var actionResult = evaluator.IsApplicable(middleware, MessageKinds.Action);

		// Assert - Attribute (Event) should be preferred
		eventResult.ShouldBeTrue();
		actionResult.ShouldBeFalse();
	}

	#endregion

	#region FilterApplicableMiddleware Tests

	[Fact]
	public void FilterApplicableMiddleware_Should_Throw_When_MiddlewareTypes_Is_Null()
	{
		// Arrange
		var evaluator = CreateEvaluator();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			evaluator.FilterApplicableMiddleware(null!, MessageKinds.Action));
	}

	[Fact]
	public void FilterApplicableMiddleware_Should_Return_Empty_For_Empty_Input()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		var types = Enumerable.Empty<Type>();

		// Act
		var result = evaluator.FilterApplicableMiddleware(types, MessageKinds.Action);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void FilterApplicableMiddleware_Should_Return_Only_Applicable_Middleware()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		var types = new[]
		{
			typeof(ActionOnlyMiddleware),    // Should be included
			typeof(EventOnlyMiddleware),     // Should be excluded
			typeof(AllKindsMiddleware),      // Should be included
		};

		// Act
		var result = evaluator.FilterApplicableMiddleware(types, MessageKinds.Action).ToList();

		// Assert
		result.ShouldContain(typeof(ActionOnlyMiddleware));
		result.ShouldContain(typeof(AllKindsMiddleware));
		result.ShouldNotContain(typeof(EventOnlyMiddleware));
		result.Count.ShouldBe(2);
	}

	[Fact]
	public void FilterApplicableMiddleware_Should_Respect_Enabled_Features()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		var types = new[]
		{
			typeof(ActionOnlyMiddleware),       // No feature requirement
			typeof(RequiresTracingMiddleware),  // Requires Tracing
		};
		var features = new HashSet<DispatchFeatures>(); // No features enabled

		// Act
		var result = evaluator.FilterApplicableMiddleware(types, MessageKinds.Action, features).ToList();

		// Assert
		result.ShouldContain(typeof(ActionOnlyMiddleware));
		result.ShouldNotContain(typeof(RequiresTracingMiddleware));
	}

	[Fact]
	public void FilterApplicableMiddleware_Should_Handle_All_Middleware_Types()
	{
		// Arrange - Verify FilterApplicableMiddleware can process various middleware types
		var evaluator = CreateEvaluator();
		var types = new[]
		{
			typeof(ActionOnlyMiddleware),
			typeof(EventOnlyMiddleware),
			typeof(AllKindsMiddleware),
			typeof(NoAttributeMiddleware),
		};

		// Act
		var result = evaluator.FilterApplicableMiddleware(types, MessageKinds.All).ToList();

		// Assert - All should match for MessageKinds.All
		result.ShouldContain(typeof(AllKindsMiddleware));
		result.ShouldContain(typeof(NoAttributeMiddleware));
	}

	[Fact]
	public void FilterApplicableMiddleware_Should_Apply_Feature_Filtering()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		var types = new[]
		{
			typeof(ActionOnlyMiddleware),
			typeof(RequiresTracingMiddleware),
			typeof(RequiresMultipleFeaturesMiddleware),
		};
		var features = new HashSet<DispatchFeatures> { DispatchFeatures.Tracing };

		// Act
		var result = evaluator.FilterApplicableMiddleware(types, MessageKinds.Action, features).ToList();

		// Assert
		result.ShouldContain(typeof(ActionOnlyMiddleware)); // No feature requirement
		result.ShouldContain(typeof(RequiresTracingMiddleware)); // Has Tracing
		result.ShouldNotContain(typeof(RequiresMultipleFeaturesMiddleware)); // Missing Metrics
	}

	#endregion

	#region Cache Freezing Tests (PERF-13/PERF-14)

	[Fact]
	public void IsCacheFrozen_Should_Be_False_Initially()
	{
		// Assert
		MiddlewareApplicabilityEvaluator.IsCacheFrozen.ShouldBeFalse();
	}

	[Fact]
	public void FreezeCache_Should_Set_IsCacheFrozen_To_True()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		// Populate cache by calling IsApplicable
		_ = evaluator.IsApplicable(typeof(ActionOnlyMiddleware), MessageKinds.Action);

		// Act
		MiddlewareApplicabilityEvaluator.FreezeCache();

		// Assert
		MiddlewareApplicabilityEvaluator.IsCacheFrozen.ShouldBeTrue();
	}

	[Fact]
	public void FreezeCache_Should_Be_Idempotent()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		_ = evaluator.IsApplicable(typeof(ActionOnlyMiddleware), MessageKinds.Action);

		// Act
		MiddlewareApplicabilityEvaluator.FreezeCache();
		MiddlewareApplicabilityEvaluator.FreezeCache();
		MiddlewareApplicabilityEvaluator.FreezeCache();

		// Assert - Should still be frozen
		MiddlewareApplicabilityEvaluator.IsCacheFrozen.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_Should_Still_Work_After_Cache_Frozen()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		_ = evaluator.IsApplicable(typeof(ActionOnlyMiddleware), MessageKinds.Action);
		MiddlewareApplicabilityEvaluator.FreezeCache();

		// Act - Query same middleware type
		var cachedResult = evaluator.IsApplicable(typeof(ActionOnlyMiddleware), MessageKinds.Action);

		// Assert
		cachedResult.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_Should_Handle_Cache_Miss_After_Freeze()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		_ = evaluator.IsApplicable(typeof(ActionOnlyMiddleware), MessageKinds.Action);
		MiddlewareApplicabilityEvaluator.FreezeCache();

		// Act - Query a new middleware type not in the frozen cache
		var result = evaluator.IsApplicable(typeof(EventOnlyMiddleware), MessageKinds.Event);

		// Assert - Should still return correct result (built but not cached)
		result.ShouldBeTrue();
	}

	[Fact]
	public void ClearCache_Should_Reset_Frozen_State()
	{
		// Arrange
		var evaluator = CreateEvaluator();
		_ = evaluator.IsApplicable(typeof(ActionOnlyMiddleware), MessageKinds.Action);
		MiddlewareApplicabilityEvaluator.FreezeCache();

		// Act
		MiddlewareApplicabilityEvaluator.ClearCache();

		// Assert
		MiddlewareApplicabilityEvaluator.IsCacheFrozen.ShouldBeFalse();
	}

	#endregion

	#region Test Middleware Classes

	private class NoAttributeMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
			=> new(new TestMessageResult(true));
	}

	[AppliesTo(MessageKinds.Action)]
	private class ActionOnlyMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
			=> new(new TestMessageResult(true));
	}

	[AppliesTo(MessageKinds.Event)]
	private class EventOnlyMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.Event;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
			=> new(new TestMessageResult(true));
	}

	[AppliesTo(MessageKinds.All)]
	private class AllKindsMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
			=> new(new TestMessageResult(true));
	}

	[ExcludeKinds(MessageKinds.Event)]
	private class ExcludeEventsMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
			=> new(new TestMessageResult(true));
	}

	[AppliesTo(MessageKinds.Action)]
	[RequiresFeatures(DispatchFeatures.Tracing)]
	private class RequiresTracingMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
			=> new(new TestMessageResult(true));
	}

	[AppliesTo(MessageKinds.Action)]
	[RequiresFeatures(DispatchFeatures.Tracing, DispatchFeatures.Metrics)]
	private class RequiresMultipleFeaturesMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
			=> new(new TestMessageResult(true));
	}

	[AppliesTo(MessageKinds.Event)]
	private class EventOnlyMiddlewareInstance : IDispatchMiddleware
	{
		// Interface says Action, but attribute says Event
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
			=> new(new TestMessageResult(true));
	}

	private sealed class TestMessageResult(bool succeeded) : IMessageResult
	{
		public bool Succeeded { get; } = succeeded;
		public IMessageProblemDetails? ProblemDetails { get; }
		public Dispatch.Abstractions.Routing.RoutingDecision? RoutingDecision { get; }
		public object? ValidationResult { get; }
		public object? AuthorizationResult { get; }
		public bool CacheHit { get; }
		public string? ErrorMessage { get; }
	}

	#endregion
}
