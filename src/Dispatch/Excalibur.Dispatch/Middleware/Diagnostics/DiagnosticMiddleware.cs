// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Middleware.PipelineDiagnostics;

/// <summary>
/// Diagnostic middleware that logs pipeline state including registered middleware,
/// their stage assignments, and the message being dispatched.
/// </summary>
/// <remarks>
/// <para>
/// This middleware is intended for development and debugging only. It logs at
/// <see cref="LogLevel.Debug"/> level and should be registered at the start of the pipeline
/// to capture the full pipeline state before any processing occurs.
/// </para>
/// <para>
/// Register via <c>builder.UseDiagnostics()</c>.
/// </para>
/// </remarks>
internal sealed partial class DiagnosticMiddleware : IDispatchMiddleware
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<DiagnosticMiddleware> _logger;
	private volatile bool _pipelineLogged;

	/// <summary>
	/// Initializes a new instance of the <see cref="DiagnosticMiddleware"/> class.
	/// </summary>
	/// <param name="serviceProvider">The service provider for resolving middleware registrations.</param>
	/// <param name="logger">The logger instance.</param>
	public DiagnosticMiddleware(
		IServiceProvider serviceProvider,
		ILogger<DiagnosticMiddleware> logger)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;

	/// <inheritdoc />
	public ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		if (_logger.IsEnabled(LogLevel.Debug))
		{
			LogMessageDispatched(message.GetType().Name);

			if (!_pipelineLogged)
			{
				LogPipelineState();
				_pipelineLogged = true;
			}
		}

		return nextDelegate(message, context, cancellationToken);
	}

	private void LogPipelineState()
	{
		var middlewares = _serviceProvider.GetServices<IDispatchMiddleware>().ToList();
		LogPipelineMiddlewareCount(middlewares.Count);

		var ordered = middlewares
			.OrderBy(static m => m.Stage ?? DispatchMiddlewareStage.End)
			.ThenBy(static m => m.GetType().Name, StringComparer.Ordinal);

		foreach (var middleware in ordered)
		{
			var stageName = middleware.Stage?.ToString() ?? "Unassigned";
			LogMiddlewareRegistration(middleware.GetType().Name, stageName);
		}
	}

	[LoggerMessage(2210, LogLevel.Debug,
		"[Diagnostics] Dispatching message '{MessageType}'")]
	private partial void LogMessageDispatched(string messageType);

	[LoggerMessage(2211, LogLevel.Debug,
		"[Diagnostics] Pipeline has {MiddlewareCount} registered middleware")]
	private partial void LogPipelineMiddlewareCount(int middlewareCount);

	[LoggerMessage(2212, LogLevel.Debug,
		"[Diagnostics] Middleware: {MiddlewareName} -> Stage: {Stage}")]
	private partial void LogMiddlewareRegistration(string middlewareName, string stage);
}
