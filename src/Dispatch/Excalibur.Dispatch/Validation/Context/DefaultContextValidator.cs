// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Validation;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Validation.Context;

/// <summary>
/// Default implementation of context validator that performs standard validation checks.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="DefaultContextValidator" /> class. </remarks>
/// <param name="logger"> The logger for diagnostic output. </param>
/// <param name="options"> Configuration options for validation. </param>
public sealed partial class DefaultContextValidator(
	ILogger<DefaultContextValidator> logger,
	IOptions<ContextValidationOptions> options) : IContextValidator
{
	/// <summary>
	/// Cached composite formats for performance.
	/// </summary>
	private static readonly CompositeFormat MissingRequiredFieldsFormat = CompositeFormat.Parse(ErrorConstants.MissingRequiredFields);

	private static readonly CompositeFormat CorruptedFieldsDetectedFormat = CompositeFormat.Parse(ErrorConstants.CorruptedFieldsDetected);

	private readonly ILogger<DefaultContextValidator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ContextValidationOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc />
	public ValueTask<ContextValidationResult> ValidateAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var missingFields = new List<string>();
		var corruptedFields = new List<string>();
		var details = new Dictionary<string, object?>(StringComparer.Ordinal);

		// Validate required fields
		if (_options.ValidateRequiredFields)
		{
			ValidateRequiredFields(context, missingFields, corruptedFields, details);
		}

		// Validate multi-tenancy fields if enabled
		if (_options.ValidateMultiTenancy && message is ITenantAware)
		{
			ValidateMultiTenancyFields(context, missingFields, corruptedFields, details);
		}

		// Validate authentication fields if enabled
		if (_options.ValidateAuthentication)
		{
			ValidateAuthenticationFields(context, missingFields, corruptedFields, details);
		}

		// Validate distributed tracing context if enabled
		if (_options.ValidateTracing)
		{
			ValidateTracingContext(context, missingFields, corruptedFields, details);
		}

		// Validate message versioning if enabled
		if (_options.ValidateVersioning)
		{
			ValidateVersioning(context, missingFields, corruptedFields, details);
		}

		// Validate collection integrity if enabled
		if (_options.ValidateCollections)
		{
			ValidateCollections(context, corruptedFields, details);
		}

		// Validate correlation chain if enabled
		if (_options.ValidateCorrelationChain)
		{
			ValidateCorrelationChain(context, missingFields, corruptedFields, details);
		}

		// Check message age if configured
		if (_options.MaxMessageAge.HasValue)
		{
			ValidateMessageAge(context, corruptedFields, details);
		}

		// Apply custom field validation rules
		foreach (var rule in _options.FieldValidationRules)
		{
			ValidateField(context, rule.Key, rule.Value, missingFields, corruptedFields, details);
		}

		// Determine overall result
		if (missingFields.Count == 0 && corruptedFields.Count == 0)
		{
			return ValueTask.FromResult(ContextValidationResult.Success());
		}

		var severity = DetermineSeverity(missingFields, corruptedFields);
		var reason = BuildFailureReason(missingFields, corruptedFields);

		var result = new ContextValidationResult
		{
			IsValid = false,
			FailureReason = reason,
			MissingFields = missingFields,
			CorruptedFields = corruptedFields,
			Details = details,
			Severity = severity,
		};

		return ValueTask.FromResult(result);
	}

	/// <summary>
	/// Determines the severity based on the validation issues found.
	/// </summary>
	private static ValidationSeverity DetermineSeverity(List<string> missingFields, List<string> corruptedFields)
	{
		// Critical if core fields are missing or corrupted
		if (missingFields.Contains("MessageId", StringComparer.Ordinal) || missingFields.Contains("MessageType", StringComparer.Ordinal) ||
			corruptedFields.Contains("MessageId", StringComparer.Ordinal) || corruptedFields.Contains("MessageType", StringComparer.Ordinal))
		{
			return ValidationSeverity.Critical;
		}

		// Error if any required fields are missing or corrupted
		if (missingFields.Count > 0 || corruptedFields.Count > 0)
		{
			return ValidationSeverity.Error;
		}

		return ValidationSeverity.Info;
	}

	/// <summary>
	/// Builds a human-readable failure reason.
	/// </summary>
	private static string BuildFailureReason(List<string> missingFields, List<string> corruptedFields)
	{
		var parts = new List<string>();

		if (missingFields.Count > 0)
		{
			parts.Add(string.Format(CultureInfo.InvariantCulture, MissingRequiredFieldsFormat, string.Join(", ", missingFields)));
		}

		if (corruptedFields.Count > 0)
		{
			parts.Add(string.Format(CultureInfo.InvariantCulture, CorruptedFieldsDetectedFormat, string.Join(", ", corruptedFields)));
		}

		return string.Join(". ", parts);
	}

	/// <summary>
	/// Validates MessageId format.
	/// </summary>
	private static bool IsValidMessageId(string messageId)
	{
		// Check for common formats: GUID, UUID, or reasonable string
		if (string.IsNullOrWhiteSpace(messageId))
		{
			return false;
		}

		// Check if it's a valid GUID
		if (Guid.TryParse(messageId, out _))
		{
			return true;
		}

		// Check reasonable length (not too short, not too long)
		return messageId.Length is >= 3 and <= 256;
	}

	/// <summary>
	/// Validates correlation ID format.
	/// </summary>
	private static bool IsValidCorrelationId(string correlationId) =>

		// Similar to MessageId validation
		IsValidMessageId(correlationId);

	/// <summary>
	/// Validates W3C TraceContext format.
	/// </summary>
	private static bool IsValidTraceParent(string traceParent)
	{
		// W3C TraceContext format: version-trace-id-parent-id-trace-flags
		// Example: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01
		if (string.IsNullOrWhiteSpace(traceParent))
		{
			return false;
		}

		var parts = traceParent.Split('-');
		if (parts.Length != 4)
		{
			return false;
		}

		// Version (2 hex chars)
		if (parts[0].Length != 2)
		{
			return false;
		}

		// Trace ID (32 hex chars)
		if (parts[1].Length != 32)
		{
			return false;
		}

		// Parent ID (16 hex chars)
		if (parts[2].Length != 16)
		{
			return false;
		}

		// Trace flags (2 hex chars)
		if (parts[3].Length != 2)
		{
			return false;
		}

		return true;
	}

	/// <summary>
	/// Validates required fields are present and valid.
	/// </summary>
	private void ValidateRequiredFields(
		IMessageContext context,
		List<string> missingFields,
		List<string> corruptedFields,
		Dictionary<string, object?> details)
	{
		// Check MessageId
		if (string.IsNullOrWhiteSpace(context.MessageId))
		{
			missingFields.Add("MessageId");
		}
		else if (!IsValidMessageId(context.MessageId))
		{
			corruptedFields.Add("MessageId");
			details["MessageId_Value"] = context.MessageId;
		}

		// Check MessageType
		if (string.IsNullOrWhiteSpace(context.MessageType))
		{
			missingFields.Add("MessageType");
		}

		// Check any additional required fields from configuration
		foreach (var fieldName in _options.RequiredFields)
		{
			if (!context.Items.TryGetValue(fieldName, out var value) || value is null)
			{
				if (!missingFields.Contains(fieldName, StringComparer.Ordinal))
				{
					missingFields.Add(fieldName);
				}
			}
		}
	}

	/// <summary>
	/// Validates multi-tenancy fields.
	/// </summary>
	private static void ValidateMultiTenancyFields(
		IMessageContext context,
		List<string> missingFields,
		List<string> corruptedFields,
		Dictionary<string, object?> details)
	{
		if (context.TenantId is null)
		{
			missingFields.Add("TenantId");
		}
		else if (string.IsNullOrWhiteSpace(context.TenantId))
		{
			corruptedFields.Add("TenantId");
			details["TenantId_Empty"] = true;
		}
	}

	/// <summary>
	/// Validates authentication fields.
	/// </summary>
	private static void ValidateAuthenticationFields(
		IMessageContext context,
		List<string> missingFields,
		List<string> corruptedFields,
		Dictionary<string, object?> details)
	{
		_ = missingFields;

		// Check if UserId is present when authentication is expected
		if (!string.IsNullOrWhiteSpace(context.UserId))
		{
			// Validate UserId format if present
			if (context.UserId.Length > 256)
			{
				corruptedFields.Add("UserId");
				details["UserId_TooLong"] = context.UserId.Length;
			}
		}

		// Check authorization result consistency
		if (context.AuthorizationResult() is IAuthorizationResult { IsAuthorized: false })
		{
			details["Authorization_Failed"] = true;
		}
	}

	/// <summary>
	/// Validates distributed tracing context.
	/// </summary>
	private static void ValidateTracingContext(
		IMessageContext context,
		List<string> missingFields,
		List<string> corruptedFields,
		Dictionary<string, object?> details)
	{
		_ = missingFields;
		if (!string.IsNullOrWhiteSpace(context.TraceParent))
		{
			// Validate W3C TraceContext format
			if (!IsValidTraceParent(context.TraceParent))
			{
				corruptedFields.Add("TraceParent");
				details["TraceParent_Invalid"] = context.TraceParent;
			}
		}

		// Check if Activity.Current exists and matches
		var activity = Activity.Current;
		if (activity != null && !string.IsNullOrWhiteSpace(context.TraceParent))
		{
			var activityTraceParent = activity.Id;
			if (activityTraceParent?.StartsWith(context.TraceParent, StringComparison.Ordinal) == false)
			{
				details["TraceContext_Mismatch"] = true;
				details["Activity_Id"] = activityTraceParent;
				details["Context_TraceParent"] = context.TraceParent;
			}
		}
	}

	/// <summary>
	/// Validates message versioning consistency.
	/// </summary>
	private static void ValidateVersioning(
		IMessageContext context,
		List<string> missingFields,
		List<string> corruptedFields,
		Dictionary<string, object?> details)
	{
		_ = missingFields;

		// Check version metadata consistency
		if (context.VersionMetadata() != null)
		{
			var versionMetadata = context.VersionMetadata() as IMessageVersionMetadata;
			if (!string.IsNullOrWhiteSpace(context.MessageVersion()) &&
				versionMetadata?.Version > 0 &&
!string.Equals(context.MessageVersion(), versionMetadata?.Version.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
			{
				corruptedFields.Add("MessageVersion");
				details["Version_Mismatch"] = true;
				details["Context_Version"] = context.MessageVersion();
				details["Metadata_Version"] = versionMetadata?.Version;
			}
		}

		// Validate desired version if specified
		var desiredVersion = context.DesiredVersion();
		if (desiredVersion != null && int.TryParse(desiredVersion, out var versionInt) && versionInt < 0)
		{
			corruptedFields.Add("DesiredVersion");
			details["DesiredVersion_Invalid"] = desiredVersion;
		}
	}

	/// <summary>
	/// Validates collection integrity.
	/// </summary>
	private static void ValidateCollections(
		IMessageContext context,
		List<string> corruptedFields,
		Dictionary<string, object?> details)
	{
		// Check Items collection
		if (context.Items == null)
		{
			corruptedFields.Add("Items");
			details["Items_Null"] = true;
		}
	}

	/// <summary>
	/// Validates correlation chain integrity.
	/// </summary>
	private static void ValidateCorrelationChain(
		IMessageContext context,
		List<string> missingFields,
		List<string> corruptedFields,
		Dictionary<string, object?> details)
	{
		_ = missingFields;

		// Validate CorrelationId format
		if (context.CorrelationId != null && !string.IsNullOrWhiteSpace(context.CorrelationId))
		{
			var correlationIdStr = context.CorrelationId;
			if (!IsValidCorrelationId(correlationIdStr))
			{
				corruptedFields.Add("CorrelationId");
				details["CorrelationId_Invalid"] = correlationIdStr;
			}
		}

		// Validate CausationId if present
		if (context.CausationId != null && !string.IsNullOrWhiteSpace(context.CausationId))
		{
			var causationIdStr = context.CausationId;
			if (!IsValidCorrelationId(causationIdStr))
			{
				corruptedFields.Add("CausationId");
				details["CausationId_Invalid"] = causationIdStr;
			}
		}
	}

	/// <summary>
	/// Validates message age.
	/// </summary>
	private void ValidateMessageAge(
		IMessageContext context,
		List<string> corruptedFields,
		Dictionary<string, object?> details)
	{
		var now = DateTimeOffset.UtcNow;
		var messageAge = now - context.ReceivedTimestampUtc;

		if (messageAge > _options.MaxMessageAge!.Value)
		{
			corruptedFields.Add("MessageAge");
			details["MessageAge_Hours"] = messageAge.TotalHours;
			details["MaxAge_Hours"] = _options.MaxMessageAge.Value.TotalHours;
		}

		// Check for future timestamps (clock skew)
		if (context.SentTimestampUtc > DateTimeOffset.UtcNow.AddMinutes(5))
		{
			corruptedFields.Add("SentTimestampUtc");
			details["SentTimestamp_Future"] = context.SentTimestampUtc.Value;
		}
	}

	/// <summary>
	/// Validates a specific field using custom rules.
	/// </summary>
	private void ValidateField(
		IMessageContext context,
		string fieldName,
		FieldValidationRule rule,
		List<string> missingFields,
		List<string> corruptedFields,
		Dictionary<string, object?> details)
	{
		var value = context.Items.TryGetValue(fieldName, out var fieldValue) ? fieldValue : null;

		// Check if required
		if (rule.Required && value == null)
		{
			missingFields.Add(fieldName);
			return;
		}

		if (value == null)
		{
			return;
		}

		// Check type
		if (rule.ExpectedType?.IsInstanceOfType(value) == false)
		{
			corruptedFields.Add(fieldName);
			details[$"{fieldName}_TypeMismatch"] = value.GetType().Name;
			return;
		}

		// String validations
		if (value is string stringValue)
		{
			if (rule.MinLength.HasValue && stringValue.Length < rule.MinLength.Value)
			{
				corruptedFields.Add(fieldName);
				details[$"{fieldName}_TooShort"] = stringValue.Length;
			}

			if (rule.MaxLength.HasValue && stringValue.Length > rule.MaxLength.Value)
			{
				corruptedFields.Add(fieldName);
				details[$"{fieldName}_TooLong"] = stringValue.Length;
			}

			if (!string.IsNullOrWhiteSpace(rule.Pattern))
			{
				try
				{
					if (!Regex.IsMatch(stringValue, rule.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)))
					{
						corruptedFields.Add(fieldName);
						details[$"{fieldName}_PatternMismatch"] = true;
					}
				}
				catch (RegexMatchTimeoutException)
				{
					LogRegexTimeout(fieldName, rule.Pattern);
				}
			}
		}

		// Custom validator
		if (rule.CustomValidator != null && !rule.CustomValidator(value))
		{
			corruptedFields.Add(fieldName);
			details[$"{fieldName}_CustomValidation"] = rule.ErrorMessage ?? ErrorConstants.CustomValidationFailed;
		}
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.ValidationRegexTimeout, LogLevel.Warning,
		"Regex timeout validating field {FieldName} with pattern {Pattern}")]
	private partial void LogRegexTimeout(string fieldName, string pattern);
}
