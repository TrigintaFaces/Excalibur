// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Logging;

namespace AuditLoggingSample.Middleware;

/// <summary>
/// Pipeline middleware that logs all command/query execution for compliance auditing.
/// Demonstrates field redaction for sensitive data like PII and PCI fields.
/// </summary>
public sealed partial class AuditLoggingMiddleware : IDispatchMiddleware
{
	private readonly ISecurityEventLogger _securityEventLogger;
	private readonly ILogger<AuditLoggingMiddleware> _logger;

	// Fields to redact in audit logs (PII, PCI data)
	private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
	{
		"Password",
		"Secret",
		"Token",
		"CreditCard",
		"CreditCardNumber",
		"CardNumber",
		"CVV",
		"CVC",
		"SocialSecurityNumber",
		"SSN",
		"TaxId",
		"BankAccount",
		"RoutingNumber",
	};

	// Regex patterns for common sensitive data
	private static readonly Regex EmailPattern = EmailRegex();
	private static readonly Regex PhonePattern = PhoneRegex();
	private static readonly Regex CreditCardPattern = CreditCardRegex();
	private static readonly Regex SsnPattern = SsnRegex();

	public AuditLoggingMiddleware(
		ISecurityEventLogger securityEventLogger,
		ILogger<AuditLoggingMiddleware> logger)
	{
		_securityEventLogger = securityEventLogger;
		_logger = logger;
	}

	/// <summary>
	/// Gets the pipeline stage for audit logging (runs late in the pipeline).
	/// </summary>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;

	/// <summary>
	/// Gets the message kinds this middleware applies to (all messages).
	/// </summary>
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		var messageType = message.GetType().Name;
		var startTime = DateTimeOffset.UtcNow;

		// Log command/query start
		_logger.LogInformation(
			"[AUDIT] Starting {MessageType} at {Timestamp}",
			messageType,
			startTime);

		// Create redacted payload for audit log
		var redactedPayload = RedactSensitiveData(message);

		try
		{
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
			var duration = DateTimeOffset.UtcNow - startTime;

			// Log successful execution
			await _securityEventLogger.LogSecurityEventAsync(
				SecurityEventType.AuditLogAccess,
				$"Command executed: {messageType} (Duration: {duration.TotalMilliseconds:F0}ms)",
				SecuritySeverity.Low,
				cancellationToken,
				context).ConfigureAwait(false);

			_logger.LogInformation(
				"[AUDIT] Completed {MessageType} in {Duration}ms. Payload: {Payload}",
				messageType,
				duration.TotalMilliseconds,
				redactedPayload);

			return result;
		}
		catch (UnauthorizedAccessException ex)
		{
			// Log authorization failure
			await _securityEventLogger.LogSecurityEventAsync(
				SecurityEventType.AuthorizationFailure,
				$"Unauthorized access attempt: {messageType} - {ex.Message}",
				SecuritySeverity.High,
				cancellationToken,
				context).ConfigureAwait(false);

			_logger.LogWarning(
				"[AUDIT] Authorization failed for {MessageType}: {Error}",
				messageType,
				ex.Message);

			throw;
		}
		catch (Exception ex)
		{
			var duration = DateTimeOffset.UtcNow - startTime;

			// Log execution failure
			await _securityEventLogger.LogSecurityEventAsync(
				SecurityEventType.ValidationError,
				$"Command failed: {messageType} - {ex.Message}",
				SecuritySeverity.Medium,
				cancellationToken,
				context).ConfigureAwait(false);

			_logger.LogError(
				ex,
				"[AUDIT] Failed {MessageType} in {Duration}ms. Payload: {Payload}",
				messageType,
				duration.TotalMilliseconds,
				redactedPayload);

			throw;
		}
	}

	/// <summary>
	/// Redacts sensitive fields from the message payload for safe audit logging.
	/// </summary>
	private static string RedactSensitiveData(object message)
	{
		try
		{
			var jsonOptions = new JsonSerializerOptions
			{
				WriteIndented = false,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			};

			var json = JsonSerializer.Serialize(message, jsonOptions);

			// Redact known sensitive field names
			foreach (var field in SensitiveFields)
			{
				// Match patterns like "fieldName": "value" or "fieldName":"value"
				json = Regex.Replace(
					json,
					$@"(""{field}""\s*:\s*)""[^""]*""",
					$@"$1""[REDACTED]""",
					RegexOptions.IgnoreCase);
			}

			// Redact email addresses
			json = EmailPattern.Replace(json, "[EMAIL REDACTED]");

			// Redact phone numbers
			json = PhonePattern.Replace(json, "[PHONE REDACTED]");

			// Redact credit card numbers (13-19 digits)
			json = CreditCardPattern.Replace(json, "[CARD REDACTED]");

			// Redact SSN patterns
			json = SsnPattern.Replace(json, "[SSN REDACTED]");

			return json;
		}
		catch
		{
			return "[Serialization Error]";
		}
	}

	[GeneratedRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
	private static partial Regex EmailRegex();

	[GeneratedRegex(@"\+?1?[-.\s]?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}", RegexOptions.Compiled)]
	private static partial Regex PhoneRegex();

	[GeneratedRegex(@"\b\d{13,19}\b", RegexOptions.Compiled)]
	private static partial Regex CreditCardRegex();

	[GeneratedRegex(@"\b\d{3}[-.\s]?\d{2}[-.\s]?\d{4}\b", RegexOptions.Compiled)]
	private static partial Regex SsnRegex();
}
