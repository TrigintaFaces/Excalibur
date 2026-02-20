// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

/// <summary>
/// Unit tests for the <see cref="MiddlewareApplicabilityEvaluator"/> class.
/// </summary>
/// <remarks>
/// Sprint 461 - Task S461.1: Remaining 0% Coverage Tests.
/// Tests middleware applicability evaluation based on message kinds and features.
/// Implements requirements R2.4-R2.6 verification.
/// </remarks>
[Collection("HandlerInvokerRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class MiddlewareApplicabilityEvaluatorShould : IDisposable
{
	private readonly IOptions<MiddlewareApplicabilityOptions> _options;
	private readonly ILogger<MiddlewareApplicabilityEvaluator> _logger;
	private readonly MiddlewareApplicabilityEvaluator _sut;

	public MiddlewareApplicabilityEvaluatorShould()
	{
		_options = A.Fake<IOptions<MiddlewareApplicabilityOptions>>();
		_ = A.CallTo(() => _options.Value).Returns(new MiddlewareApplicabilityOptions());
		_logger = A.Fake<ILogger<MiddlewareApplicabilityEvaluator>>();
		_sut = new MiddlewareApplicabilityEvaluator(_options, _logger);

		// Clear the static cache before each test
		MiddlewareApplicabilityEvaluator.ClearCache();
	}

	public void Dispose()
	{
		// Clear the static cache after each test
		MiddlewareApplicabilityEvaluator.ClearCache();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsOnNullOptions()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MiddlewareApplicabilityEvaluator(null!, _logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullLogger()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MiddlewareApplicabilityEvaluator(_options, null!));
	}

	[Fact]
	public void Constructor_InitializesSuccessfully()
	{
		// Act
		var evaluator = new MiddlewareApplicabilityEvaluator(_options, _logger);

		// Assert
		_ = evaluator.ShouldNotBeNull();
	}

	#endregion

	#region IsApplicable(Type, MessageKinds) Tests - R2.4/R2.5

	[Fact]
	public void IsApplicable_ThrowsOnNullMiddlewareType()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.IsApplicable((Type)null!, MessageKinds.Action));
	}

	[Fact]
	public void IsApplicable_ReturnsTrueForMiddlewareWithoutAttributes()
	{
		// Middleware without attributes defaults to MessageKinds.All
		// Act
		var result = _sut.IsApplicable(typeof(PlainMiddleware), MessageKinds.Action);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_ReturnsTrueForMatchingAppliesTo()
	{
		// Act
		var result = _sut.IsApplicable(typeof(ActionOnlyMiddleware), MessageKinds.Action);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_ReturnsFalseForNonMatchingAppliesTo()
	{
		// Act
		var result = _sut.IsApplicable(typeof(ActionOnlyMiddleware), MessageKinds.Event);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsApplicable_R25_ExcludeOverridesInclude()
	{
		// R2.5: Exclude overrides include
		// Act
		var result = _sut.IsApplicable(typeof(AllExceptEventsMiddleware), MessageKinds.Event);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsApplicable_R25_ExcludeDoesNotAffectOtherKinds()
	{
		// R2.5: Exclude only affects specified kinds
		// Act
		var result = _sut.IsApplicable(typeof(AllExceptEventsMiddleware), MessageKinds.Action);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_SupportsMultipleMessageKinds()
	{
		// Act
		var actionResult = _sut.IsApplicable(typeof(ActionAndEventMiddleware), MessageKinds.Action);
		var eventResult = _sut.IsApplicable(typeof(ActionAndEventMiddleware), MessageKinds.Event);
		var documentResult = _sut.IsApplicable(typeof(ActionAndEventMiddleware), MessageKinds.Document);

		// Assert
		actionResult.ShouldBeTrue();
		eventResult.ShouldBeTrue();
		documentResult.ShouldBeFalse();
	}

	#endregion

	#region IsApplicable(Type, MessageKinds, IReadOnlySet<DispatchFeatures>) Tests - R2.6

	[Fact]
	public void IsApplicable_WithFeatures_ThrowsOnNullEnabledFeatures()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.IsApplicable(typeof(PlainMiddleware), MessageKinds.Action, null!));
	}

	[Fact]
	public void IsApplicable_R26_ReturnsFalseWhenRequiredFeatureNotEnabled()
	{
		// R2.6: Middleware requires feature that is not enabled
		// Arrange
		var enabledFeatures = new HashSet<DispatchFeatures> { DispatchFeatures.Metrics };

		// Act
		var result = _sut.IsApplicable(typeof(RequiresOutboxMiddleware), MessageKinds.Action, enabledFeatures);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsApplicable_R26_ReturnsTrueWhenRequiredFeatureEnabled()
	{
		// R2.6: Middleware requires feature that is enabled
		// Arrange
		var enabledFeatures = new HashSet<DispatchFeatures> { DispatchFeatures.Outbox };

		// Act
		var result = _sut.IsApplicable(typeof(RequiresOutboxMiddleware), MessageKinds.Action, enabledFeatures);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_R26_ReturnsFalseWhenAnyRequiredFeatureNotEnabled()
	{
		// R2.6: All required features must be enabled
		// Arrange
		var enabledFeatures = new HashSet<DispatchFeatures> { DispatchFeatures.Outbox };

		// Act
		var result = _sut.IsApplicable(typeof(RequiresMultipleFeaturesMiddleware), MessageKinds.Action, enabledFeatures);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsApplicable_R26_ReturnsTrueWhenAllRequiredFeaturesEnabled()
	{
		// R2.6: All required features must be enabled
		// Arrange
		var enabledFeatures = new HashSet<DispatchFeatures> { DispatchFeatures.Outbox, DispatchFeatures.Inbox };

		// Act
		var result = _sut.IsApplicable(typeof(RequiresMultipleFeaturesMiddleware), MessageKinds.Action, enabledFeatures);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_WithFeatures_ChecksMessageKindFirst()
	{
		// Even if features are enabled, message kind must match first
		// Arrange
		var enabledFeatures = new HashSet<DispatchFeatures> { DispatchFeatures.Outbox };

		// Act
		var result = _sut.IsApplicable(typeof(ActionOnlyWithRetryMiddleware), MessageKinds.Event, enabledFeatures);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region IsApplicable(IDispatchMiddleware, MessageKinds) Tests

	[Fact]
	public void IsApplicable_WithInstance_ThrowsOnNullMiddleware()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.IsApplicable((IDispatchMiddleware)null!, MessageKinds.Action));
	}

	[Fact]
	public void IsApplicable_WithInstance_PrefersAttributeOverInterface()
	{
		// Arrange
		var middleware = new AttributeOverridesInterfaceMiddleware();

		// Act - Attribute says Action only, interface says All
		var actionResult = _sut.IsApplicable(middleware, MessageKinds.Action);
		var eventResult = _sut.IsApplicable(middleware, MessageKinds.Event);

		// Assert
		actionResult.ShouldBeTrue();
		eventResult.ShouldBeFalse();
	}

	[Fact]
	public void IsApplicable_WithInstance_FallsBackToInterfaceWhenNoAttribute()
	{
		// Arrange
		var middleware = new InterfaceOnlyMiddleware();

		// Act - Interface says Event only
		var eventResult = _sut.IsApplicable(middleware, MessageKinds.Event);
		var actionResult = _sut.IsApplicable(middleware, MessageKinds.Action);

		// Assert
		eventResult.ShouldBeTrue();
		actionResult.ShouldBeFalse();
	}

	#endregion

	#region FilterApplicableMiddleware Tests

	[Fact]
	public void FilterApplicableMiddleware_ThrowsOnNullMiddlewareTypes()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.FilterApplicableMiddleware(null!, MessageKinds.Action));
	}

	[Fact]
	public void FilterApplicableMiddleware_FiltersCorrectly()
	{
		// Arrange
		var middlewareTypes = new[]
		{
			typeof(PlainMiddleware),
			typeof(ActionOnlyMiddleware),
			typeof(EventOnlyMiddleware),
		};

		// Act
		var result = _sut.FilterApplicableMiddleware(middlewareTypes, MessageKinds.Action).ToList();

		// Assert
		result.ShouldContain(typeof(PlainMiddleware));
		result.ShouldContain(typeof(ActionOnlyMiddleware));
		result.ShouldNotContain(typeof(EventOnlyMiddleware));
	}

	[Fact]
	public void FilterApplicableMiddleware_WithFeatures_FiltersCorrectly()
	{
		// Arrange
		var middlewareTypes = new[]
		{
			typeof(PlainMiddleware),
			typeof(RequiresOutboxMiddleware),
		};
		var enabledFeatures = new HashSet<DispatchFeatures>(); // No features enabled

		// Act
		var result = _sut.FilterApplicableMiddleware(middlewareTypes, MessageKinds.Action, enabledFeatures).ToList();

		// Assert
		result.ShouldContain(typeof(PlainMiddleware));
		result.ShouldNotContain(typeof(RequiresOutboxMiddleware));
	}

	[Fact]
	public void FilterApplicableMiddleware_ReturnsEmptyForEmptyInput()
	{
		// Arrange
		var middlewareTypes = Array.Empty<Type>();

		// Act
		var result = _sut.FilterApplicableMiddleware(middlewareTypes, MessageKinds.Action).ToList();

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void FilterApplicableMiddleware_ReturnsAllApplicableForMessageKindAll()
	{
		// Arrange
		var middlewareTypes = new[]
		{
			typeof(PlainMiddleware),
			typeof(ActionOnlyMiddleware),
			typeof(EventOnlyMiddleware),
		};

		// Act - PlainMiddleware applies to All, so it applies to Action|Event|Document
		var result = _sut.FilterApplicableMiddleware(middlewareTypes, MessageKinds.All).ToList();

		// Assert - PlainMiddleware matches All, others need specific kinds
		result.ShouldContain(typeof(PlainMiddleware));
	}

	#endregion

	#region Cache Tests (PERF-13/PERF-14)

	[Fact]
	public void IsCacheFrozen_InitiallyFalse()
	{
		// Assert
		MiddlewareApplicabilityEvaluator.IsCacheFrozen.ShouldBeFalse();
	}

	[Fact]
	public void FreezeCache_SetsCacheFrozenToTrue()
	{
		// Arrange - Warm up cache
		_ = _sut.IsApplicable(typeof(PlainMiddleware), MessageKinds.Action);

		// Act
		MiddlewareApplicabilityEvaluator.FreezeCache();

		// Assert
		MiddlewareApplicabilityEvaluator.IsCacheFrozen.ShouldBeTrue();
	}

	[Fact]
	public void FreezeCache_IsIdempotent()
	{
		// Arrange - Warm up cache
		_ = _sut.IsApplicable(typeof(PlainMiddleware), MessageKinds.Action);

		// Act
		MiddlewareApplicabilityEvaluator.FreezeCache();
		MiddlewareApplicabilityEvaluator.FreezeCache();
		MiddlewareApplicabilityEvaluator.FreezeCache();

		// Assert
		MiddlewareApplicabilityEvaluator.IsCacheFrozen.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_WorksAfterCacheFreeze()
	{
		// Arrange - Warm up cache
		_ = _sut.IsApplicable(typeof(PlainMiddleware), MessageKinds.Action);

		// Act
		MiddlewareApplicabilityEvaluator.FreezeCache();
		var result = _sut.IsApplicable(typeof(PlainMiddleware), MessageKinds.Action);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsApplicable_HandlesCacheMissAfterFreeze()
	{
		// Arrange - Freeze with PlainMiddleware only
		_ = _sut.IsApplicable(typeof(PlainMiddleware), MessageKinds.Action);
		MiddlewareApplicabilityEvaluator.FreezeCache();

		// Act - Query for a type not in the frozen cache
		var result = _sut.IsApplicable(typeof(ActionOnlyMiddleware), MessageKinds.Action);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ClearCache_ResetsCacheState()
	{
		// Arrange
		_ = _sut.IsApplicable(typeof(PlainMiddleware), MessageKinds.Action);
		MiddlewareApplicabilityEvaluator.FreezeCache();
		MiddlewareApplicabilityEvaluator.IsCacheFrozen.ShouldBeTrue();

		// Act
		MiddlewareApplicabilityEvaluator.ClearCache();

		// Assert
		MiddlewareApplicabilityEvaluator.IsCacheFrozen.ShouldBeFalse();
	}

	#endregion

	#region Test Middleware Types

	private class PlainMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			return next(message, context, cancellationToken);
		}
	}

	[AppliesTo(MessageKinds.Action)]
	private class ActionOnlyMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			return next(message, context, cancellationToken);
		}
	}

	[AppliesTo(MessageKinds.Event)]
	private class EventOnlyMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			return next(message, context, cancellationToken);
		}
	}

	[AppliesTo(MessageKinds.Action | MessageKinds.Event)]
	private class ActionAndEventMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			return next(message, context, cancellationToken);
		}
	}

	[ExcludeKinds(MessageKinds.Event)]
	private class AllExceptEventsMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			return next(message, context, cancellationToken);
		}
	}

	[RequiresFeatures(DispatchFeatures.Outbox)]
	private class RequiresOutboxMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			return next(message, context, cancellationToken);
		}
	}

	[RequiresFeatures(DispatchFeatures.Outbox, DispatchFeatures.Inbox)]
	private class RequiresMultipleFeaturesMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			return next(message, context, cancellationToken);
		}
	}

	[AppliesTo(MessageKinds.Action)]
	[RequiresFeatures(DispatchFeatures.Outbox)]
	private class ActionOnlyWithRetryMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			return next(message, context, cancellationToken);
		}
	}

	[AppliesTo(MessageKinds.Action)]
	private class AttributeOverridesInterfaceMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All; // Interface says All, but attribute says Action

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			return next(message, context, cancellationToken);
		}
	}

	private class InterfaceOnlyMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.Event; // Interface says Event only

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			return next(message, context, cancellationToken);
		}
	}

	#endregion
}
