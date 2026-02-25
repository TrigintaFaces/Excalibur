// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace DispatchMinimal.Middleware;

/// <summary>
/// Example custom middleware that logs message dispatch.
/// Demonstrates how to intercept the pipeline and add cross-cutting concerns.
/// </summary>
public class LoggingMiddleware : IDispatchMiddleware
{
	/// <summary>
	/// The pipeline stage where this middleware runs.
	/// </summary>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Logging;

	/// <summary>
	/// Which message types this middleware applies to.
	/// </summary>
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		var messageType = message.GetType().Name;
		var correlationId = context.CorrelationId;

		Console.WriteLine();
		Console.WriteLine($"[LoggingMiddleware] >>> Dispatching: {messageType}");
		Console.WriteLine($"[LoggingMiddleware]     Correlation: {correlationId}");

		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		try
		{
			// Call the next middleware/handler in the pipeline
			var result = await nextDelegate(message, context, cancellationToken);

			stopwatch.Stop();
			Console.WriteLine($"[LoggingMiddleware] <<< Completed: {messageType} in {stopwatch.ElapsedMilliseconds}ms (Success: {result.Succeeded})");

			return result;
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			Console.WriteLine($"[LoggingMiddleware] !!! Failed: {messageType} in {stopwatch.ElapsedMilliseconds}ms");
			Console.WriteLine($"[LoggingMiddleware]     Error: {ex.Message}");
			throw;
		}
	}
}
