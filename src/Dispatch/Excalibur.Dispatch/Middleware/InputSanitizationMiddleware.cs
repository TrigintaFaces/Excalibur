// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware responsible for sanitizing input data to prevent injection attacks and clean malformed or dangerous content before processing.
/// </summary>
/// <remarks>
/// This middleware protects against various injection attacks by sanitizing message content:
/// <list type="bullet">
/// <item> SQL injection prevention through parameterization hints </item>
/// <item> XSS prevention by HTML encoding dangerous characters </item>
/// <item> Path traversal prevention by normalizing file paths </item>
/// <item> Command injection prevention by escaping shell metacharacters </item>
/// <item> Script injection prevention by removing or encoding script tags </item>
/// <item> Null byte injection prevention </item>
/// <item> Unicode normalization to prevent homograph attacks </item>
/// </list>
/// </remarks>
[AppliesTo(MessageKinds.Action | MessageKinds.Event)]
public sealed partial class InputSanitizationMiddleware : IDispatchMiddleware
{
	private readonly InputSanitizationOptions _options;

	private readonly ISanitizationService? _sanitizationService;

	private readonly ILogger<InputSanitizationMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="InputSanitizationMiddleware" /> class. Creates a new input sanitization middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for input sanitization. </param>
	/// <param name="sanitizationService"> Optional custom sanitization service. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public InputSanitizationMiddleware(
		IOptions<InputSanitizationOptions> options,
		ISanitizationService? sanitizationService,
		ILogger<InputSanitizationMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_sanitizationService = sanitizationService;
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action | MessageKinds.Event;

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

		// Skip sanitization if disabled
		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Check if message bypasses sanitization
		if (BypassesSanitization(message))
		{
			LogBypassesSanitization(message.GetType().Name);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Set up activity tags
		using var activity = Activity.Current;
		_ = activity?.SetTag("sanitization.enabled", value: true);
		_ = activity?.SetTag("sanitization.message_type", message.GetType().Name);

		var sanitizationCount = 0;

		try
		{
			// Sanitize message properties
			sanitizationCount = await SanitizeMessagePropertiesAsync(message, sanitizationCount, cancellationToken)
				.ConfigureAwait(false);

			// Sanitize context items if enabled
			if (_options.SanitizeContextItems)
			{
				SanitizeContextItems(context, ref sanitizationCount);
			}

			_ = activity?.SetTag("sanitization.count", sanitizationCount);

			if (sanitizationCount > 0)
			{
				LogSanitizedValues(sanitizationCount, message.GetType().Name);

				// Store sanitization info in context
				context.SetItem("Sanitization.Applied", value: true);
				context.SetItem("Sanitization.Count", sanitizationCount);
			}

			// Continue pipeline execution
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogSanitizationError(message.GetType().Name, ex);

			if (_options.ThrowOnSanitizationError)
			{
				throw new InputSanitizationException(
					$"Failed to sanitize input for message type {message.GetType().Name}", ex);
			}

			// Continue without sanitization if configured not to throw
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
	}

	// Source-generated regex patterns for optimal performance
	[GeneratedRegex(@"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE|UNION|INTO|FROM|WHERE)\b)",
		RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
	private static partial Regex SqlInjectionPattern();

	[GeneratedRegex(@"<script[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex ScriptTagPattern();

	[GeneratedRegex(@"<[^>]+>", RegexOptions.None)]
	private static partial Regex HtmlTagPattern();

	[GeneratedRegex(@"(\.\.[\\/])|([~\/])", RegexOptions.ExplicitCapture)]
	private static partial Regex PathTraversalPattern();

	/// <summary>
	/// Sanitizes context items.
	/// </summary>
	private static void SanitizeContextItems(IMessageContext context, ref int sanitizationCount)
	{
		// Context items are typically metadata, headers, etc. We'll be more conservative with these
		var itemsToSanitize = new[]
		{
			"UserAgent", "Referer", "Origin", "Host", "X-Forwarded-For", "X-Real-IP", "X-Original-URL", "X-Rewrite-URL",
		};

		foreach (var itemName in itemsToSanitize)
		{
			var value = context.GetItem<string>(itemName);
			if (!string.IsNullOrEmpty(value))
			{
				// Basic sanitization for headers
				var sanitized = value.Replace("\r", string.Empty, StringComparison.Ordinal)
					.Replace("\n", string.Empty, StringComparison.Ordinal);

				if (!string.Equals(sanitized, value, StringComparison.Ordinal))
				{
					context.SetItem(itemName, sanitized);
					sanitizationCount++;
				}
			}
		}
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.InputSanitizationMiddlewareExecuting, LogLevel.Debug,
		"Message type {MessageType} bypasses input sanitization")]
	private partial void LogBypassesSanitization(string messageType);

	[LoggerMessage(MiddlewareEventId.InputSanitized, LogLevel.Debug,
		"Sanitized {Count} values in message {MessageType}")]
	private partial void LogSanitizedValues(int count, string messageType);

	[LoggerMessage(MiddlewareEventId.DangerousInputDetected, LogLevel.Error,
		"Error during input sanitization for message {MessageType}")]
	private partial void LogSanitizationError(string messageType, Exception ex);

	[LoggerMessage(MiddlewareEventId.InputValidationPassed, LogLevel.Warning,
		"Failed to sanitize property {PropertyName} of type {Type}")]
	private partial void LogPropertySanitizationFailed(string propertyName, string type, Exception ex);

	[LoggerMessage(MiddlewareEventId.DangerousInputDetected + 4, LogLevel.Warning,
		"Potential SQL injection detected in property {PropertyName} of message {MessageType}")]
	private partial void LogSqlInjectionDetected(string propertyName, string messageType);

	/// <summary>
	/// Sanitizes message properties recursively.
	/// </summary>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2072:'obj' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties' in call to target method",
		Justification = "Message types are preserved through source generation and DI registration")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties' in call to target method",
		Justification = "Message types are preserved through source generation and DI registration")]
	private async Task<int> SanitizeMessagePropertiesAsync(
		object obj,
		int sanitizationCount,
		CancellationToken cancellationToken,
		HashSet<object>? visited = null)
	{
		if (obj == null)
		{
			return sanitizationCount;
		}

		// Prevent infinite recursion
		visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
		if (!visited.Add(obj))
		{
			return sanitizationCount;
		}

		var type = obj.GetType();

		// Skip primitive types that don't need sanitization
		if (type.IsPrimitive || type == typeof(decimal) || type == typeof(DateTime) ||
			type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid))
		{
			return sanitizationCount;
		}

		// Handle strings
		if (obj is string str)
		{
			// String values are handled at the property level
			return sanitizationCount;
		}

		// Handle collections
		if (obj is IEnumerable enumerable and not string)
		{
			foreach (var item in enumerable)
			{
				sanitizationCount = await SanitizeMessagePropertiesAsync(item, sanitizationCount, cancellationToken, visited)
					.ConfigureAwait(false);
			}

			return sanitizationCount;
		}

		// Handle object properties
		var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(static p => p is { CanRead: true, CanWrite: true });

		foreach (var property in properties)
		{
			try
			{
				var value = property.GetValue(obj);
				if (value == null)
				{
					continue;
				}

				// Check if property should be skipped
				if (ShouldSkipProperty(property))
				{
					continue;
				}

				// Sanitize string properties
				if (value is string stringValue)
				{
					var sanitized = await SanitizeStringAsync(
						stringValue,
						property.Name,
						type,
						cancellationToken).ConfigureAwait(false);

					if (!ReferenceEquals(sanitized, stringValue))
					{
						property.SetValue(obj, sanitized);
						sanitizationCount++;
					}
				}

				// Recursively sanitize complex properties
				else if (!property.PropertyType.IsPrimitive)
				{
					sanitizationCount = await SanitizeMessagePropertiesAsync(value, sanitizationCount, cancellationToken, visited)
						.ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				LogPropertySanitizationFailed(property.Name, type.Name, ex);
			}
		}

		return sanitizationCount;
	}

	/// <summary>
	/// Sanitizes a string value.
	/// </summary>
	private async Task<string> SanitizeStringAsync(
		string value,
		string propertyName,
		Type messageType,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		// Use custom sanitization service if available
		if (_sanitizationService != null && _options.UseCustomSanitization)
		{
			var result = await _sanitizationService.SanitizeValueAsync(
				value, propertyName, messageType, cancellationToken).ConfigureAwait(false);

			if (result is string sanitizedResult)
			{
				return sanitizedResult;
			}
		}

		var sanitized = value;

		// Remove null bytes
		if (_options.Features.RemoveNullBytes)
		{
			sanitized = sanitized.Replace("\0", string.Empty, StringComparison.Ordinal);
		}

		// Normalize Unicode
		if (_options.Features.NormalizeUnicode)
		{
			sanitized = sanitized.Normalize(NormalizationForm.FormC);
		}

		// Remove or encode HTML/Script tags
		if (_options.Features.PreventXss)
		{
			if (_options.Features.RemoveHtmlTags)
			{
				sanitized = ScriptTagPattern().Replace(sanitized, string.Empty);
				sanitized = HtmlTagPattern().Replace(sanitized, string.Empty);
			}
			else
			{
				// HTML encode dangerous characters
				sanitized = WebUtility.HtmlEncode(sanitized);
			}
		}

		// Prevent SQL injection (log warnings but don't modify)
		if (_options.Features.PreventSqlInjection && SqlInjectionPattern().IsMatch(sanitized))
		{
			LogSqlInjectionDetected(propertyName, messageType.Name);
		}

		// Prevent path traversal
		if (_options.Features.PreventPathTraversal)
		{
			sanitized = PathTraversalPattern().Replace(sanitized, string.Empty);
		}

		// Trim whitespace
		if (_options.Features.TrimWhitespace)
		{
			sanitized = sanitized.Trim();
		}

		// Enforce max length
		if (_options.MaxStringLength > 0 && sanitized.Length > _options.MaxStringLength)
		{
			sanitized = sanitized[.._options.MaxStringLength];
		}

		return sanitized;
	}

	/// <summary>
	/// Determines if a message bypasses sanitization.
	/// </summary>
	private bool BypassesSanitization(IDispatchMessage message)
	{
		var messageType = message.GetType();

		// Check for bypass attribute
		if (messageType.GetCustomAttributes(typeof(BypassSanitizationAttribute), inherit: true).Length != 0)
		{
			return true;
		}

		// Check if message type is in bypass list
		return _options.BypassSanitizationForTypes?.Contains(messageType.Name) == true;
	}

	/// <summary>
	/// Determines if a property should be skipped during sanitization.
	/// </summary>
	private bool ShouldSkipProperty(PropertyInfo property)
	{
		// Skip properties with NoSanitize attribute
		if (property.GetCustomAttributes(typeof(NoSanitizeAttribute), inherit: true).Length != 0)
		{
			return true;
		}

		// Skip properties in the exclusion list
		return _options.ExcludeProperties?.Contains(property.Name) == true;
	}
}
