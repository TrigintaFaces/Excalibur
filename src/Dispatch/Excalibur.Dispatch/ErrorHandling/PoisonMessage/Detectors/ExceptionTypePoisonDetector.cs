// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Detects poison messages based on specific exception types.
/// </summary>
public sealed class ExceptionTypePoisonDetector : IPoisonMessageDetector
{
	private readonly PoisonMessageOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="ExceptionTypePoisonDetector" /> class.
	/// </summary>
	/// <param name="options"> The poison message configuration options. </param>
	public ExceptionTypePoisonDetector(IOptions<PoisonMessageOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
	}

	/// <inheritdoc />
	public Task<PoisonDetectionResult> IsPoisonMessageAsync(
		IDispatchMessage message,
		IMessageContext context,
		MessageProcessingInfo processingInfo,
		Exception? exception = null)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(processingInfo);

		if (exception == null)
		{
			return Task.FromResult(PoisonDetectionResult.NotPoison());
		}

		// Check if the exception is in the transient list (should not poison)
		var exceptionType = exception.GetType();
		if (IsTransientException(exceptionType))
		{
			return Task.FromResult(PoisonDetectionResult.NotPoison());
		}

		// Check if the exception is in the poison list
		if (IsPoisonException(exceptionType))
		{
			var details = new Dictionary<string, object>
(StringComparer.Ordinal)
			{
				["ExceptionType"] = exceptionType.FullName ?? exceptionType.Name,
				["ExceptionMessage"] = exception.Message,
				["AttemptCount"] = processingInfo.AttemptCount,
			};

			if (exception.InnerException != null)
			{
				details["InnerExceptionType"] = exception.InnerException.GetType().FullName ?? exception.InnerException.GetType().Name;
				details["InnerExceptionMessage"] = exception.InnerException.Message;
			}

			return Task.FromResult(
				PoisonDetectionResult.Poison(
					$"Message processing failed with non-transient exception: {exceptionType.Name}",
					nameof(ExceptionTypePoisonDetector),
					details));
		}

		return Task.FromResult(PoisonDetectionResult.NotPoison());
	}

	/// <summary>
	/// Determines whether an exception type is considered transient.
	/// </summary>
	private bool IsTransientException(Type exceptionType) =>
		_options.TransientExceptionTypes.Any(transientType =>
			transientType.IsAssignableFrom(exceptionType));

	/// <summary>
	/// Determines whether an exception type should poison a message.
	/// </summary>
	private bool IsPoisonException(Type exceptionType) =>
		_options.PoisonExceptionTypes.Any(poisonType =>
			poisonType.IsAssignableFrom(exceptionType));
}
