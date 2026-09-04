// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Telemetry;
using Excalibur.Dispatch.Observability.Metrics;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="TracingMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class TracingMiddlewareShould : IDisposable
{
	private readonly ITelemetrySanitizer _fakeSanitizer = A.Fake<ITelemetrySanitizer>();
	private readonly ActivityListener _listener;

	private static IOptions<ObservabilityOptions> DefaultOptions =>
		Microsoft.Extensions.Options.Options.Create(new ObservabilityOptions { IncludeSensitiveData = true });

	public TracingMiddlewareShould()
	{
		_listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
		};
		ActivitySource.AddActivityListener(_listener);
	}

	// The listener is the only thing this fixture owns. Activities are owned -- and disposed --
	// by the code that started them. This listener is process-global (it matches any source named
	// "Dispatch"), so it also observes activities started by test classes running in parallel;
	// disposing those would stop another test's span mid-flight.
	public void Dispose() => _listener.Dispose();

	/// <summary>
	/// Creates a fake <see cref="IMessageContext"/> backed by a real Items dictionary
	/// so that extension methods (GetItem, SetItem, ContainsItem) work correctly.
	/// </summary>
	private static IMessageContext CreateFakeContext(Dictionary<string, object>? items = null)
	{
		var context = A.Fake<IMessageContext>();
		var itemsDict = items ?? new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(itemsDict);
		A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		return context;
	}

	[Fact]
	public void ThrowOnNullSanitizer()
	{
		Should.Throw<ArgumentNullException>(() => new TracingMiddleware(DefaultOptions, null!));
	}

	[Fact]
	public void HavePreProcessingStage()
	{
		var middleware = new TracingMiddleware(DefaultOptions, _fakeSanitizer);
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public async Task InvokeNextDelegate_AndReturnResult()
	{
		// Arrange
		var middleware = new TracingMiddleware(DefaultOptions, _fakeSanitizer);
		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		var expectedResult = A.Fake<IMessageResult>();
		A.CallTo(() => expectedResult.IsSuccess).Returns(true);

		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		var middleware = new TracingMiddleware(DefaultOptions, _fakeSanitizer);
		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(null!, A.Fake<IMessageContext>(), next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		var middleware = new TracingMiddleware(DefaultOptions, _fakeSanitizer);
		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullNextDelegate()
	{
		var middleware = new TracingMiddleware(DefaultOptions, _fakeSanitizer);

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(
				A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), null!, CancellationToken.None));
	}
}
