// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware that catches exceptions from downstream handlers and converts them to problem details.
/// </summary>
/// <remarks>
/// <para>
/// This middleware intercepts exceptions thrown during message processing and uses the
/// <see cref="IExceptionMapper"/> service to convert them to RFC 7807 Problem Details format.
/// </para>
/// <para>
/// Important: <see cref="OperationCanceledException"/> is never mapped and is always re-thrown
/// to allow proper cancellation propagation.
/// </para>
/// </remarks>
/// <param name="mapper"> The exception mapper service. </param>
/// <param name="logger"> The logger instance. </param>
[AppliesTo(MessageKinds.All)]
public sealed partial class ExceptionMappingMiddleware(
	IExceptionMapper mapper,
	ILogger<ExceptionMappingMiddleware> logger) : IDispatchMiddleware
{
	private readonly IExceptionMapper _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

	private readonly ILogger<ExceptionMappingMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.ErrorHandling;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		try
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Never map cancellation - propagate up for proper cancellation handling
			throw;
		}
		catch (Exception ex)
		{
			return await MapExceptionToResultAsync(ex, context, cancellationToken).ConfigureAwait(false);
		}
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.ExceptionMapped, LogLevel.Warning,
		"Exception mapped to HTTP {StatusCode}: {ErrorType}")]
	private partial void LogExceptionMapped(int statusCode, string errorType, Exception ex);

	[LoggerMessage(MiddlewareEventId.UnhandledExceptionCaught, LogLevel.Error,
		"Failed to map exception for message {MessageId}")]
	private partial void LogExceptionMappingError(string messageId, Exception ex);

	private async Task<IMessageResult> MapExceptionToResultAsync(
		Exception exception,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			var problemDetails = await _mapper.MapAsync(exception, cancellationToken).ConfigureAwait(false);

			LogExceptionMapped(
				problemDetails.ErrorCode != 0 ? problemDetails.ErrorCode : 500,
				problemDetails.Type ?? "unknown",
				exception);

			return MessageResult.Failed(problemDetails);
		}
		catch (Exception mappingException)
		{
			// If mapping itself fails, return a generic error
			LogExceptionMappingError(context.MessageId ?? "unknown", mappingException);

			var fallbackProblemDetails = new MessageProblemDetails
			{
				Type = ProblemDetailsTypes.MappingFailed,
				Title = "Exception Mapping Failed",
				ErrorCode = 500,
				Status = 500,
				Detail = "An error occurred while processing the exception. The original error has been logged.",
				Instance = $"urn:dispatch:exception:{Guid.NewGuid()}",
			};

			return MessageResult.Failed(fallbackProblemDetails);
		}
	}
}
