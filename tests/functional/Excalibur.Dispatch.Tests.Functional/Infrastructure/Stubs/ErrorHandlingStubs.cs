// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#nullable enable

using Excalibur.Dispatch.Abstractions.Features;

using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Tests.Shared.TestTypes
{
	/// <summary>
	/// Stub ErrorHandlingOptions for backward compatibility with legacy tests.
	/// </summary>
	public sealed class ErrorHandlingOptions
	{
		public bool Enabled { get; set; } = true;
		public bool ThrowExceptions { get; set; }
		public IDictionary<Type, Func<Exception, IMessageResult>> ExceptionHandlers { get; init; } = new Dictionary<Type, Func<Exception, IMessageResult>>();
	}

	/// <summary>
	/// Stub ErrorHandlingMiddleware for backward compatibility with legacy tests.
	/// </summary>
	public sealed class ErrorHandlingMiddleware
	{
		private readonly ErrorHandlingOptions _options;
		private readonly ILogger<ErrorHandlingMiddleware> _logger;

		public ErrorHandlingMiddleware(
			IOptions<ErrorHandlingOptions> options,
			ILogger<ErrorHandlingMiddleware> logger)
		{
			_options = options.Value;
			_logger = logger;
		}

		public async Task<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> next,
			CancellationToken cancellationToken)
		{
			if (!_options.Enabled)
			{
				return await next(message, context, cancellationToken).ConfigureAwait(false);
			}

			try
			{
				return await next(message, context, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				var errorId = Guid.NewGuid().ToString("N");
				context.Items["__Error"] = ex;
				context.Items["__ErrorId"] = errorId;

				var traceParent = context.GetTraceParent();
				_logger.LogError(ex, "Error processing message {MessageId} {TraceParent}: {ErrorMessage}",
					context.MessageId, traceParent, ex.Message);

				var problemDetails = new MessageProblemDetails
				{
					Title = "Unhandled dispatch exception",
					Detail = ex.Message,
					Instance = traceParent ?? context.MessageId,
					Status = 500,
				};

				context.Items["__Problem"] = problemDetails;

				if (_options.ThrowExceptions)
				{
					throw;
				}

				return MessageResult.Failed(problemDetails);
			}
		}
	}
}

namespace Tests.Shared.TestTypes.Messages
{
	/// <summary>
	/// Base class for test messages.
	/// </summary>
	public abstract class BaseTestMessage : IDispatchMessage
	{
		public string MessageId { get; set; } = Guid.NewGuid().ToString();
	}
}
