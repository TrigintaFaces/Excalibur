// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Logging;

using MessageProblemDetails = Excalibur.Dispatch.Abstractions.MessageProblemDetails;
using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Middleware that provides comprehensive input validation for all messages passing through the dispatch pipeline. This middleware prevents
/// injection attacks, validates data integrity, and enforces security policies.
/// </summary>
/// <param name="logger">The logger instance for diagnostics and monitoring.</param>
/// <param name="options">The input validation configuration options.</param>
/// <param name="validators">The collection of custom validators to apply.</param>
/// <param name="securityEventLogger">The security event logger for audit trail.</param>
/// <remarks> Initializes a new instance of the <see cref="InputValidationMiddleware" /> class. </remarks>
public sealed partial class InputValidationMiddleware(
	ILogger<InputValidationMiddleware> logger,
	InputValidationOptions options,
	IEnumerable<IInputValidator> validators,
	ISecurityEventLogger securityEventLogger) : IDispatchMiddleware
{
	private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

	private readonly ILogger<InputValidationMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly InputValidationOptions _options = options ?? throw new ArgumentNullException(nameof(options));
	private readonly List<IInputValidator> _validators = validators?.ToList() ?? throw new ArgumentNullException(nameof(validators));

	private readonly ISecurityEventLogger _securityEventLogger =
		securityEventLogger ?? throw new ArgumentNullException(nameof(securityEventLogger));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:Using RequiresDynamicCode member in AOT",
			Justification = "Input validation inspects message properties at runtime.")]
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
			Justification = "Input validation uses JSON serialization for message inspection.")]
	public async ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate,
	CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Skip validation if disabled (should only be in development/testing)
		if (!_options.EnableValidation)
		{
			LogValidationDisabled();
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var validationContext = new ValidationContext(message, context);

		try
		{
			// Validate message structure
			ValidateMessageStructure(message, validationContext);

			// Validate message context
			ValidateMessageContext(context, validationContext);

			// Run custom validators
			await RunCustomValidatorsAsync(message, context, validationContext).ConfigureAwait(false);

			// Check for injection attacks
			ValidateAgainstInjectionAttacks(message, validationContext);

			// Validate data size limits
			ValidateDataSizeLimits(message, validationContext);

			// If validation failed, handle according to policy
			if (validationContext.HasErrors)
			{
				await HandleValidationFailureAsync(validationContext, cancellationToken).ConfigureAwait(false);
				return MessageResult.Failed(MessageProblemDetails.ValidationError("The message failed input validation checks"));
			}

			// Add validation metadata to context
			context.Items["Validation:Passed"] = true;
			context.Items["Validation:Timestamp"] = DateTimeOffset.UtcNow;

			// Continue pipeline
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogUnexpectedValidationError(message.GetType().Name, ex);
			await _securityEventLogger.LogSecurityEventAsync(
				SecurityEventType.ValidationError,
				$"Validation error for {message.GetType().Name}: {ex.Message}",
				SecuritySeverity.Medium,
				cancellationToken,
				context).ConfigureAwait(false);
			throw;
		}
	}

	private static bool ContainsControlCharacters(string value) =>
		value.Any(static c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');

	private static bool ContainsHtmlContent(string value) =>

		// Basic HTML tag detection
		HtmlTagRegex().IsMatch(value) ||
		value.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
		value.Contains("onclick", StringComparison.OrdinalIgnoreCase) ||
		value.Contains("onerror", StringComparison.OrdinalIgnoreCase);

	private static bool IsValidUserId(string userId) =>

		// Validate user ID format (adjust based on your requirements)
		// Example: GUID format or alphanumeric with specific length
		Guid.TryParse(userId, out _) ||
		UserIdFormatRegex().IsMatch(userId);

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2072", Justification = "Runtime type inspection is required for message validation depth calculation")]
	private static int CalculateObjectDepth(object obj, int maxDepth, int currentDepth = 0)
	{
		// Stop recursion once we've exceeded the configured depth limit
		if (obj == null || currentDepth > maxDepth)
		{
			return currentDepth;
		}

		var type = obj.GetType();
		if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
		{
			return currentDepth;
		}

		var deepest = currentDepth;

		// IL2075: GetProperties called on runtime type - safe for message validation
		foreach (var property in GetPropertiesForValidation(type))
		{
			if (!property.CanRead)
			{
				continue;
			}

			var value = property.GetValue(obj);
			if (value != null)
			{
				var depth = CalculateObjectDepth(value, maxDepth, currentDepth + 1);
				deepest = Math.Max(deepest, depth);
			}
		}

		return deepest;
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075", Justification = "Message types are known at runtime and properties are accessed for validation")]
	private static PropertyInfo[] GetPropertiesForValidation([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
		=> PropertyCache.GetOrAdd(type, static t => t.GetProperties());

	// Regex patterns for injection detection
	[GeneratedRegex(@"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|UNION|ALTER|CREATE|EXEC|EXECUTE)\b)|(--)|(;)|(\*/)|(/\*)",
		RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
	private static partial Regex SqlInjectionRegex();

	[GeneratedRegex(@"(\$where|\$regex|\$ne|\$gt|\$lt|\$gte|\$lte|\$in|\$nin|\$or|\$and|\$not|\$nor)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
	private static partial Regex NoSqlInjectionRegex();

	[GeneratedRegex(@"(;|\||&|`|\$\(|<|>|\\n|\\r)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
	private static partial Regex CommandInjectionRegex();

	[GeneratedRegex(@"(\.\./|\.\.\\|%2e%2e%2f|%252e%252e%252f)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
	private static partial Regex PathTraversalRegex();

	[GeneratedRegex(@"(\*|\(|\)|\||&|=|!|~|\*\)|\(\*)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
	private static partial Regex LdapInjectionRegex();

	[GeneratedRegex("<[^>]+>", RegexOptions.NonBacktracking)]
	private static partial Regex HtmlTagRegex();

	[GeneratedRegex("^[a-zA-Z0-9]{3,50}$", RegexOptions.NonBacktracking)]
	private static partial Regex UserIdFormatRegex();

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.InputValidationDisabled, LogLevel.Warning,
		"Input validation is disabled. This should only occur in development environments.")]
	private partial void LogValidationDisabled();

	[LoggerMessage(SecurityEventId.InputValidationUnexpectedError, LogLevel.Error,
		"Unexpected error during input validation for message {MessageType}")]
	private partial void LogUnexpectedValidationError(string messageType, Exception ex);

	[LoggerMessage(SecurityEventId.InputValidatorFailed, LogLevel.Warning,
		"Custom validator {Validator} failed")]
	private partial void LogCustomValidatorFailed(string validator, Exception ex);

	[LoggerMessage(SecurityEventId.InputValidationFailed, LogLevel.Warning,
		"Input validation failed for message {MessageType} with {ErrorCount} errors. Suspicious: {IsSuspicious}")]
	private partial void LogValidationFailed(string messageType, int errorCount, bool isSuspicious);

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2072", Justification = "Runtime type inspection is required for message structure validation")]
	private void ValidateMessageStructure(IDispatchMessage message, ValidationContext validationContext)
	{
		// Check for null properties
		// IL2075: GetProperties called on runtime type - safe for message validation
		foreach (var property in GetPropertiesForValidation(message.GetType()))
		{
			if (!property.CanRead)
			{
				continue;
			}

			var value = property.GetValue(message);

			// Check for null values in required properties
			var isNullable = Nullable.GetUnderlyingType(property.PropertyType) != null ||
							 !property.PropertyType.IsValueType;

			if (value == null && !isNullable && !_options.AllowNullProperties)
			{
				validationContext.AddError($"Property '{property.Name}' cannot be null");
			}

			// Validate string properties
			if (value is string stringValue)
			{
				ValidateStringProperty(property.Name, stringValue, validationContext);
			}
		}
	}

	private void ValidateStringProperty(string propertyName, string value, ValidationContext validationContext)
	{
		// Check for empty strings
		if (string.IsNullOrWhiteSpace(value) && !_options.AllowEmptyStrings)
		{
			validationContext.AddError($"Property '{propertyName}' cannot be empty");
			return;
		}

		// Check string length
		if (value.Length > _options.MaxStringLength)
		{
			validationContext.AddError($"Property '{propertyName}' exceeds maximum length of {_options.MaxStringLength.ToString(CultureInfo.InvariantCulture)}");
		}

		// Check for control characters
		if (_options.BlockControlCharacters && ContainsControlCharacters(value))
		{
			validationContext.AddError($"Property '{propertyName}' contains prohibited control characters");
		}

		// Check for HTML/Script content
		if (_options.BlockHtmlContent && ContainsHtmlContent(value))
		{
			validationContext.AddError($"Property '{propertyName}' contains prohibited HTML content");
		}
	}

	private void ValidateMessageContext(IMessageContext context, ValidationContext validationContext)
	{
		// Validate correlation ID
		if ((string.IsNullOrEmpty(context.CorrelationId) || !Guid.TryParse(context.CorrelationId, out var correlationGuid) ||
			 correlationGuid == Guid.Empty) && _options.RequireCorrelationId)
		{
			validationContext.AddError("Message context must have a valid correlation ID");
		}

		// Validate message ID
		if (string.IsNullOrEmpty(context.MessageId))
		{
			validationContext.AddError("Message context must have a valid message ID");
		}

		// Validate timestamp using the available timestamp properties
		var timestamp = context.SentTimestampUtc ?? context.ReceivedTimestampUtc;
		var now = DateTimeOffset.UtcNow;

		// Future timestamp
		if (timestamp > now.AddMinutes(5))
		{
			validationContext.AddError("Message timestamp cannot be in the future");
		}

		if (timestamp < now.AddDays(-_options.MaxMessageAgeDays))
		{
			validationContext.AddError($"Message is too old (max age: {_options.MaxMessageAgeDays.ToString(CultureInfo.InvariantCulture)} days)");
		}

		// Validate user context if present
		if (context.Items.TryGetValue("User:MessageId", out var userId) && userId is string userIdString && !IsValidUserId(userIdString))
		{
			validationContext.AddError("Invalid user ID format");
		}
	}

	private async Task RunCustomValidatorsAsync(
		IDispatchMessage message,
		IMessageContext context,
		ValidationContext validationContext)
	{
		foreach (var validator in _validators)
		{
			try
			{
				var result = await validator.ValidateAsync(message, context).ConfigureAwait(false);
				if (!result.IsValid)
				{
					foreach (var error in result.Errors)
					{
						validationContext.AddError(error);
					}
				}
			}
			catch (Exception ex)
			{
				LogCustomValidatorFailed(validator.GetType().Name, ex);
				if (_options.FailOnValidatorException)
				{
					validationContext.AddError($"Validator {validator.GetType().Name} failed: {ex.Message}");
				}
			}
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private void ValidateAgainstInjectionAttacks(IDispatchMessage message, ValidationContext validationContext)
	{
		var json = JsonSerializer.Serialize(message);

		// SQL Injection patterns
		if (_options.BlockSqlInjection && SqlInjectionRegex().IsMatch(json))
		{
			validationContext.AddError("Potential SQL injection detected");
			validationContext.IsSuspicious = true;
		}

		// NoSQL Injection patterns
		if (_options.BlockNoSqlInjection && NoSqlInjectionRegex().IsMatch(json))
		{
			validationContext.AddError("Potential NoSQL injection detected");
			validationContext.IsSuspicious = true;
		}

		// Command Injection patterns
		if (_options.BlockCommandInjection && CommandInjectionRegex().IsMatch(json))
		{
			validationContext.AddError("Potential command injection detected");
			validationContext.IsSuspicious = true;
		}

		// Path Traversal patterns
		if (_options.BlockPathTraversal && PathTraversalRegex().IsMatch(json))
		{
			validationContext.AddError("Potential path traversal detected");
			validationContext.IsSuspicious = true;
		}

		// LDAP Injection patterns
		if (_options.BlockLdapInjection && LdapInjectionRegex().IsMatch(json))
		{
			validationContext.AddError("Potential LDAP injection detected");
			validationContext.IsSuspicious = true;
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private void ValidateDataSizeLimits(IDispatchMessage message, ValidationContext validationContext)
	{
		// Serialize message to check size
		var json = JsonSerializer.Serialize(message);
		var sizeInBytes = Encoding.UTF8.GetByteCount(json);

		if (sizeInBytes > _options.MaxMessageSizeBytes)
		{
			validationContext.AddError(
				$"Message size ({sizeInBytes.ToString(CultureInfo.InvariantCulture)} bytes) exceeds maximum allowed size ({_options.MaxMessageSizeBytes.ToString(CultureInfo.InvariantCulture)} bytes)");
		}

		// Check nested object depth
		var depth = CalculateObjectDepth(message, _options.MaxObjectDepth);
		if (depth > _options.MaxObjectDepth)
		{
			validationContext.AddError($"Message object depth ({depth.ToString(CultureInfo.InvariantCulture)}) exceeds maximum allowed depth ({_options.MaxObjectDepth.ToString(CultureInfo.InvariantCulture)})");
		}
	}

	private async Task HandleValidationFailureAsync(ValidationContext validationContext, CancellationToken cancellationToken)
	{
		var severity = validationContext.IsSuspicious ? SecuritySeverity.High : SecuritySeverity.Medium;

		// Log security event
		await _securityEventLogger.LogSecurityEventAsync(
			SecurityEventType.ValidationFailure,
			$"Input validation failed: {string.Join(", ", validationContext.Errors)}",
			severity,
			cancellationToken,
			validationContext.Context).ConfigureAwait(false);

		// Log detailed information for investigation
		LogValidationFailed(
			validationContext.Message.GetType().Name,
			validationContext.Errors.Count,
			validationContext.IsSuspicious);

		// Throw exception to prevent processing
		throw new InputValidationException(
			$"Input validation failed with {validationContext.Errors.Count} errors",
			validationContext.Errors);
	}

	private sealed class ValidationContext(IDispatchMessage message, IMessageContext context)
	{
		public IDispatchMessage Message { get; } = message;

		public IMessageContext Context { get; } = context;

		public List<string> Errors { get; } = [];

		public bool IsSuspicious { get; set; }

		public bool HasErrors => Errors.Count > 0;

		public void AddError(string error) => Errors.Add(error);
	}
}
