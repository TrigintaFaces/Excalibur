// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Detects registered dispatch pipeline middleware that the streaming, progress, and document-stream
/// dispatch paths bypass, and surfaces the bypass <em>loudly</em> (ec132p).
/// </summary>
/// <remarks>
/// <para>
/// The streaming dispatch entry points (<c>DispatchStreamingAsync</c>, <c>DispatchStreamAsync</c>,
/// <c>DispatchTransformStreamAsync</c>, <c>DispatchWithProgressAsync</c>) resolve their handler from DI and
/// invoke it directly — they do <strong>not</strong> execute the <see cref="IDispatchMiddleware"/> pipeline.
/// The pipeline is typed on <c>IDispatchMessage</c> / <c>IMessageResult</c>; streamed documents
/// (<c>IDispatchDocument</c>) and their pull-based <c>IAsyncEnumerable</c> results do not fit that
/// request/response shape, so a true streaming-aware pipeline is a larger redesign tracked separately.
/// </para>
/// <para>
/// Following the advertised-capability contract ("a seam either works or fails loud — never silently
/// degrades"), this guard converts the
/// previously <em>silent</em> bypass into a loud, one-time warning that names exactly which registered
/// All/Document-scoped middleware are not applied to streamed messages, so consumers are never misled into
/// believing a cross-cutting behavior (logging, audit, exception handling, etc.) runs when it does not.
/// </para>
/// </remarks>
internal sealed partial class StreamingPipelineBypassGuard
{
	private readonly ILogger _logger;
	private readonly string[] _bypassedMiddleware;
	private int _warned;

	/// <summary>
	/// Initializes a new instance of the <see cref="StreamingPipelineBypassGuard"/> class, computing the set of
	/// bypassed middleware once from the supplied service provider.
	/// </summary>
	/// <param name="serviceProvider"> The service provider used to resolve registered middleware. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceProvider"/> is null. </exception>
	public StreamingPipelineBypassGuard(IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);

		_logger = serviceProvider.GetService<ILoggerFactory>()
			?.CreateLogger("Excalibur.Dispatch.Delivery.Pipeline.StreamingPipeline")
			?? NullLogger.Instance;

		_bypassedMiddleware = ComputeBypassedMiddleware(serviceProvider);
	}

	/// <summary>
	/// Gets the names of registered pipeline middleware that streaming dispatch paths bypass.
	/// </summary>
	/// <value> The bypassed middleware type names; empty when none apply. </value>
	public IReadOnlyList<string> BypassedMiddleware => _bypassedMiddleware;

	/// <summary>
	/// Emits a one-time warning naming the bypassed middleware, if any apply.
	/// </summary>
	/// <param name="operation"> The streaming dispatch operation being invoked. </param>
	public void WarnIfBypassed(string operation)
	{
		if (_bypassedMiddleware.Length == 0)
		{
			return;
		}

		// Warn once per dispatcher: the bypassed set is invariant for the lifetime of the container.
		if (Interlocked.Exchange(ref _warned, 1) != 0)
		{
			return;
		}

		LogStreamingMiddlewareBypassed(
			_logger,
			operation,
			_bypassedMiddleware.Length,
			string.Join(", ", _bypassedMiddleware));
	}

	private string[] ComputeBypassedMiddleware(IServiceProvider serviceProvider)
	{
		try
		{
			var evaluator = serviceProvider.GetService<IDispatchMiddlewareApplicabilityEvaluator>();
			if (evaluator is null)
			{
				return [];
			}

			var bypassed = new List<string>();
			foreach (var middleware in serviceProvider.GetServices<IDispatchMiddleware>())
			{
				// The filtered invoker is the pipeline host itself, not a bypassed cross-cutting concern.
				if (middleware is FilteredDispatchMiddlewareInvoker)
				{
					continue;
				}

				try
				{
					// Document is the message kind streamed documents would carry through the standard pipeline,
					// so All/Document-scoped middleware are exactly those a document dispatch would run.
					if (evaluator.IsApplicable(middleware, MessageKinds.Document))
					{
						bypassed.Add(middleware.GetType().Name);
					}
				}
				catch (Exception ex)
				{
					// Detection must never break dispatch; skip middleware we cannot evaluate.
					LogBypassDetectionError(_logger, middleware.GetType().Name, ex);
				}
			}

			return bypassed.Count == 0 ? [] : [.. bypassed];
		}
		catch (Exception ex)
		{
			// Detection must never break dispatch.
			LogBypassDetectionError(_logger, "<enumeration>", ex);
			return [];
		}
	}

	[LoggerMessage(
		DeliveryEventId.StreamingPipelineMiddlewareBypassed,
		LogLevel.Warning,
		"Streaming dispatch path '{Operation}' bypasses {Count} registered pipeline middleware ({Middleware}). " +
		"Streaming/Progress/document-stream paths do not execute the dispatch middleware pipeline, so these " +
		"cross-cutting behaviors are NOT applied to streamed messages. Use a non-streaming dispatch path where " +
		"these middleware must run, or track the streaming-aware pipeline work to apply them.")]
	private static partial void LogStreamingMiddlewareBypassed(ILogger logger, string operation, int count, string middleware);

	[LoggerMessage(
		DeliveryEventId.StreamingPipelineBypassDetectionError,
		LogLevel.Debug,
		"Error detecting streaming pipeline middleware bypass for {MiddlewareType}")]
	private static partial void LogBypassDetectionError(ILogger logger, string middlewareType, Exception ex);
}
