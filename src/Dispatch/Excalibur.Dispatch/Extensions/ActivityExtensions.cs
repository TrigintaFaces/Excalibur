// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Excalibur.Dispatch.Abstractions.Telemetry;

namespace Excalibur.Dispatch.Extensions;

/// <summary>
/// Extension methods for <see cref="Activity" /> to provide enhanced tracing capabilities.
/// </summary>
public static class ActivityExtensions
{
	/// <summary>
	/// Gets a sanitized error description for an exception suitable for span status and tags.
	/// </summary>
	/// <param name="exception">The exception to get a description for.</param>
	/// <param name="sanitizer">The telemetry sanitizer for PII protection.</param>
	/// <returns>A sanitized error description safe for telemetry emission.</returns>
	/// <remarks>
	/// <para>
	/// For well-known system exceptions (<see cref="SystemException"/>, <see cref="OperationCanceledException"/>,
	/// <see cref="TimeoutException"/>), only the type name is returned since these never contain PII.
	/// </para>
	/// <para>
	/// For all other exceptions, the message is sanitized using the provided <paramref name="sanitizer"/>.
	/// </para>
	/// </remarks>
	public static string GetSanitizedErrorDescription(this Exception exception, ITelemetrySanitizer sanitizer)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentNullException.ThrowIfNull(sanitizer);

		// Well-known system exceptions: type name only (no PII risk in type names)
		if (exception is SystemException or OperationCanceledException or TimeoutException)
		{
			return exception.GetType().Name;
		}

		// All other exceptions: sanitize the message
		return sanitizer.SanitizePayload(exception.Message);
	}

	/// <summary>
	/// Sets the activity status to Error with a sanitized exception description and records the exception event.
	/// </summary>
	/// <param name="activity">The activity to set error status on.</param>
	/// <param name="exception">The exception to record.</param>
	/// <param name="sanitizer">The telemetry sanitizer for PII protection.</param>
	/// <remarks>
	/// This combines <see cref="RecordSanitizedException"/> and status-setting into a single call.
	/// The exception message in the span status and event tags is sanitized to prevent PII leakage.
	/// </remarks>
	public static void SetSanitizedErrorStatus(this Activity? activity, Exception exception, ITelemetrySanitizer sanitizer)
	{
		if (activity == null || exception == null)
		{
			return;
		}

		ArgumentNullException.ThrowIfNull(sanitizer);

		var sanitizedMessage = exception.GetSanitizedErrorDescription(sanitizer);

		// Record the sanitized exception event
		var tags = new ActivityTagsCollection
		{
			["exception.type"] = exception.GetType().FullName,
			["exception.message"] = sanitizedMessage,
		};

		if (!string.IsNullOrEmpty(exception.StackTrace))
		{
			tags["exception.stacktrace"] = exception.StackTrace;
		}

		_ = activity.AddEvent(new ActivityEvent(
			"exception",
			DateTimeOffset.UtcNow,
			tags));

		// Set the status to error
		_ = activity.SetStatus(ActivityStatusCode.Error, sanitizedMessage);
	}

	/// <summary>
	/// Records an exception as an event on the activity with sanitized exception details.
	/// </summary>
	/// <param name="activity">The activity to record the exception on.</param>
	/// <param name="exception">The exception to record.</param>
	/// <param name="sanitizer">The telemetry sanitizer for PII protection.</param>
	public static void RecordSanitizedException(this Activity? activity, Exception exception, ITelemetrySanitizer sanitizer)
	{
		if (activity == null || exception == null)
		{
			return;
		}

		ArgumentNullException.ThrowIfNull(sanitizer);

		var sanitizedMessage = exception.GetSanitizedErrorDescription(sanitizer);

		var tags = new ActivityTagsCollection
		{
			["exception.type"] = exception.GetType().FullName,
			["exception.message"] = sanitizedMessage,
		};

		if (!string.IsNullOrEmpty(exception.StackTrace))
		{
			tags["exception.stacktrace"] = exception.StackTrace;
		}

		_ = activity.AddEvent(new ActivityEvent(
			"exception",
			DateTimeOffset.UtcNow,
			tags));

		if (activity.Status == ActivityStatusCode.Unset)
		{
			_ = activity.SetStatus(ActivityStatusCode.Error, sanitizedMessage);
		}
	}

	/// <summary>
	/// Records an exception as an event on the activity with standardized exception details.
	/// </summary>
	/// <param name="activity"> The activity to record the exception on. </param>
	/// <param name="exception"> The exception to record. </param>
	/// <remarks>
	/// This method adds an event to the activity with the exception details, including the exception type, message, and stack trace. It
	/// also sets the activity status to Error if not already set. This provides compatibility with OpenTelemetry's RecordException
	/// extension method when OpenTelemetry is not directly referenced.
	/// </remarks>
	public static void RecordException(this Activity? activity, Exception exception)
	{
		if (activity == null || exception == null)
		{
			return;
		}

		// Add exception event with standardized tags
		var tags = new ActivityTagsCollection
		{
			["exception.type"] = exception.GetType().FullName,
			["exception.message"] = exception.Message,
		};

		if (!string.IsNullOrEmpty(exception.StackTrace))
		{
			tags["exception.stacktrace"] = exception.StackTrace;
		}

		// Add the event with the exception details
		_ = activity.AddEvent(new ActivityEvent(
			"exception",
			DateTimeOffset.UtcNow,
			tags));

		// Set the status to error if not already set
		if (activity.Status == ActivityStatusCode.Unset)
		{
			_ = activity.SetStatus(ActivityStatusCode.Error, exception.Message);
		}
	}

	/// <summary>
	/// Records an exception as an event on the activity with additional custom tags.
	/// </summary>
	/// <param name="activity"> The activity to record the exception on. </param>
	/// <param name="exception"> The exception to record. </param>
	/// <param name="additionalTags"> Additional tags to include with the exception event. </param>
	public static void RecordException(this Activity? activity, Exception exception, IDictionary<string, object?> additionalTags)
	{
		if (activity == null || exception == null)
		{
			return;
		}

		// Add exception event with standardized tags
		var tags = new ActivityTagsCollection
		{
			["exception.type"] = exception.GetType().FullName,
			["exception.message"] = exception.Message,
		};

		if (!string.IsNullOrEmpty(exception.StackTrace))
		{
			tags["exception.stacktrace"] = exception.StackTrace;
		}

		// Add any additional tags
		if (additionalTags != null)
		{
			foreach (var tag in additionalTags)
			{
				tags[tag.Key] = tag.Value;
			}
		}

		// Add the event with the exception details
		_ = activity.AddEvent(new ActivityEvent(
			"exception",
			DateTimeOffset.UtcNow,
			tags));

		// Set the status to error if not already set
		if (activity.Status == ActivityStatusCode.Unset)
		{
			_ = activity.SetStatus(ActivityStatusCode.Error, exception.Message);
		}
	}
}
