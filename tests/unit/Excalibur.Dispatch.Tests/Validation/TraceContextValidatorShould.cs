// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Testing;
using Excalibur.Dispatch.Validation.Context;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Validation;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class TraceContextValidatorShould : IDisposable
{
	private readonly TraceContextValidator _validator;

	public TraceContextValidatorShould()
	{
		_validator = new TraceContextValidator(NullLogger<TraceContextValidator>.Instance);
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new TraceContextValidator(null!));
	}

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		// Arrange
		var context = new MessageContextBuilder().Build();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _validator.ValidateAsync(null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _validator.ValidateAsync(message, null!, CancellationToken.None));
	}

	[Fact]
	public async Task ReturnSuccess_WhenNoActivityAndNoTraceParent()
	{
		// Arrange -- ensure no Activity is current
		var previousActivity = Activity.Current;
		Activity.Current = null;
		try
		{
			var message = A.Fake<IDispatchMessage>();
			var context = new MessageContextBuilder()
				.WithMessageId("msg-123")
				.Build();
			// No TraceParent set (default is null)

			// Act
			var result = await _validator.ValidateAsync(message, context, CancellationToken.None);

			// Assert
			result.IsValid.ShouldBeTrue();
		}
		finally
		{
			Activity.Current = previousActivity;
		}
	}

	[Fact]
	public async Task DetectMissingActivity_WhenTraceParentPresent()
	{
		// Arrange -- ensure no Activity is current
		var previousActivity = Activity.Current;
		Activity.Current = null;
		try
		{
			var message = A.Fake<IDispatchMessage>();
			var context = new MessageContextBuilder()
				.WithMessageId("msg-123")
				.WithTraceParent("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01")
				.Build();

			// Act
			var result = await _validator.ValidateAsync(message, context, CancellationToken.None);

			// Assert
			result.IsValid.ShouldBeFalse();
			result.FailureReason.ShouldContain("Activity.Current is null but context has TraceParent");
		}
		finally
		{
			Activity.Current = previousActivity;
		}
	}

	[Fact]
	public async Task DetectOrphanedTraceContext_WhenTraceParentPresentButNoMessageId()
	{
		// Arrange -- ensure no Activity is current
		var previousActivity = Activity.Current;
		Activity.Current = null;
		try
		{
			var message = A.Fake<IDispatchMessage>();
			// Build context with TraceParent but override MessageId to null
			var context = new MessageContextBuilder()
				.WithTraceParent("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01")
				.Build();
			// Override auto-generated MessageId to null
			context.MessageId = null;

			// Act
			var result = await _validator.ValidateAsync(message, context, CancellationToken.None);

			// Assert
			result.IsValid.ShouldBeFalse();
			result.FailureReason.ShouldContain("TraceParent present but MessageId is missing");
		}
		finally
		{
			Activity.Current = previousActivity;
		}
	}

	[Fact]
	public async Task ReturnSuccess_WhenActivityMatchesTraceParent()
	{
		// Arrange
		using var activitySource = new ActivitySource("TestSource");
		using var listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		using var activity = activitySource.StartActivity("TestOperation");
		activity.ShouldNotBeNull();

		var traceId = activity.TraceId.ToString();
		var spanId = activity.SpanId.ToString();
		var traceParent = $"00-{traceId}-{spanId}-01";

		var correlationId = Guid.NewGuid().ToString();
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContextBuilder()
			.WithMessageId("msg-123")
			.WithCorrelationId(correlationId)
			.WithTraceParent(traceParent)
			.WithMessageType("TestMessage")
			.Build();

		// Set activity baggage to match context so validator doesn't flag mismatch
		activity.SetBaggage("correlation-id", correlationId);

		// Act
		var result = await _validator.ValidateAsync(message, context, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ImplementIContextValidator()
	{
		// Assert
		_validator.ShouldBeAssignableTo<IContextValidator>();
	}

	public void Dispose()
	{
		// No-op -- validator is not disposable
	}
}
