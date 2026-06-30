// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// Author≠impl regression lock for <c>ec132p</c> — the <see cref="StreamingPipelineBypassGuard"/> that surfaces,
/// LOUDLY, which registered middleware the streaming/progress/document-stream dispatch paths bypass.
/// </summary>
/// <remarks>
/// <para>
/// (Implementer = SoftwareArchitect — the dispatcher guard; this is the independent lock authored by
/// TestsDeveloper, against the seam SA pinned in msg 18056.)
/// </para>
/// <para>
/// <b>Non-vacuity:</b> a Document/All-scoped middleware MUST appear in <see cref="StreamingPipelineBypassGuard.BypassedMiddleware"/>
/// and the one-time warning (<see cref="DeliveryEventId.StreamingPipelineMiddlewareBypassed"/>, 40208) MUST fire —
/// so removing the guard's detection (or stubbing applicability to empty) flips these RED. An Action-only
/// middleware MUST NOT be flagged, proving the guard performs real applicability filtering rather than flagging
/// everything. The guard must also never throw on a detection failure.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StreamingPipelineBypassGuardShould
{
	[Fact]
	public void FlagDocumentScopedMiddleware_ButNotActionOnlyMiddleware()
	{
		var evaluator = CreateEvaluator();
		var loggerFactory = new RecordingLoggerFactory();
		using var provider = BuildProvider(
			evaluator, loggerFactory, new DocScopedMiddleware(), new ActionOnlyMiddleware());

		var guard = new StreamingPipelineBypassGuard(provider);

		// The Document/All-scoped middleware is flagged as bypassed; the Action-only one is NOT
		// (real applicability filtering, not "everything is bypassed").
		guard.BypassedMiddleware.ShouldContain(nameof(DocScopedMiddleware));
		guard.BypassedMiddleware.ShouldNotContain(nameof(ActionOnlyMiddleware));
	}

	[Fact]
	public void WarnExactlyOnce_WhenMiddlewareIsBypassed()
	{
		var evaluator = CreateEvaluator();
		var loggerFactory = new RecordingLoggerFactory();
		using var provider = BuildProvider(evaluator, loggerFactory, new DocScopedMiddleware());

		var guard = new StreamingPipelineBypassGuard(provider);

		// Multiple streaming entry points call WarnIfBypassed; the warning must fire once per dispatcher.
		guard.WarnIfBypassed("DispatchStreamAsync");
		guard.WarnIfBypassed("DispatchWithProgressAsync");

		loggerFactory.CountOf(DeliveryEventId.StreamingPipelineMiddlewareBypassed).ShouldBe(1);
	}

	[Fact]
	public void NotWarn_WhenNothingIsBypassed()
	{
		var evaluator = CreateEvaluator();
		var loggerFactory = new RecordingLoggerFactory();
		// Only an Action-only middleware → nothing applies to Document → empty bypass set.
		using var provider = BuildProvider(evaluator, loggerFactory, new ActionOnlyMiddleware());

		var guard = new StreamingPipelineBypassGuard(provider);

		guard.BypassedMiddleware.ShouldBeEmpty();
		guard.WarnIfBypassed("DispatchStreamAsync");
		loggerFactory.CountOf(DeliveryEventId.StreamingPipelineMiddlewareBypassed).ShouldBe(0);
	}

	[Fact]
	public void ReturnEmpty_WhenNoApplicabilityEvaluatorIsRegistered()
	{
		var loggerFactory = new RecordingLoggerFactory();
		using var provider = BuildProvider(evaluator: null, loggerFactory, new DocScopedMiddleware());

		var guard = new StreamingPipelineBypassGuard(provider);

		guard.BypassedMiddleware.ShouldBeEmpty();
	}

	[Fact]
	public void NotThrow_AndSkipOnlyTheOffender_WhenEvaluatorThrowsForOneMiddleware()
	{
		// Detection must never break dispatch: a middleware whose applicability evaluation throws is skipped,
		// while the others are still evaluated and flagged.
		var evaluator = CreateEvaluator();
		var loggerFactory = new RecordingLoggerFactory();
		using var provider = BuildProvider(
			evaluator, loggerFactory, new DocScopedMiddleware(), new ThrowingMiddleware());

		var guard = Should.NotThrow(() => new StreamingPipelineBypassGuard(provider));

		guard.BypassedMiddleware.ShouldContain(nameof(DocScopedMiddleware));
		guard.BypassedMiddleware.ShouldNotContain(nameof(ThrowingMiddleware));
	}

	// ── Helpers ──

	private static IDispatchMiddlewareApplicabilityEvaluator CreateEvaluator()
	{
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		A.CallTo(() => evaluator.IsApplicable(A<IDispatchMiddleware>._, MessageKinds.Document))
			.ReturnsLazily((IDispatchMiddleware m, MessageKinds _) =>
			{
				if (m is ThrowingMiddleware)
				{
					throw new InvalidOperationException("evaluator boom");
				}

				// Document-scoped middleware is what a document dispatch would run through the pipeline.
				return m is DocScopedMiddleware;
			});
		return evaluator;
	}

	private static ServiceProvider BuildProvider(
		IDispatchMiddlewareApplicabilityEvaluator? evaluator,
		ILoggerFactory loggerFactory,
		params IDispatchMiddleware[] middlewares)
	{
		var services = new ServiceCollection();
		services.AddSingleton(loggerFactory);
		if (evaluator is not null)
		{
			services.AddSingleton(evaluator);
		}

		foreach (var middleware in middlewares)
		{
			services.AddSingleton(middleware);
		}

		return services.BuildServiceProvider();
	}

	private sealed class DocScopedMiddleware : TestMiddlewareBase
	{
		public override MessageKinds ApplicableMessageKinds => MessageKinds.All;
	}

	private sealed class ActionOnlyMiddleware : TestMiddlewareBase
	{
		public override MessageKinds ApplicableMessageKinds => MessageKinds.Action;
	}

	private sealed class ThrowingMiddleware : TestMiddlewareBase;

	private abstract class TestMiddlewareBase : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;

		public virtual MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException("Test middleware is never invoked by the bypass-detection lock.");
	}

	private sealed class RecordingLoggerFactory : ILoggerFactory
	{
		private readonly List<EventId> _events = [];

		public int CountOf(int eventId) => _events.Count(e => e.Id == eventId);

		public ILogger CreateLogger(string categoryName) => new RecordingLogger(_events);

		public void AddProvider(ILoggerProvider provider)
		{
			// no-op
		}

		public void Dispose()
		{
			// no-op
		}

		private sealed class RecordingLogger(List<EventId> events) : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

			public bool IsEnabled(LogLevel logLevel) => true;

			public void Log<TState>(
				LogLevel logLevel,
				EventId eventId,
				TState state,
				Exception? exception,
				Func<TState, Exception?, string> formatter) =>
				events.Add(eventId);
		}
	}
}
