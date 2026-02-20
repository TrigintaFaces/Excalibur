// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageProblemDetails = Excalibur.Dispatch.Abstractions.MessageProblemDetails;
using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Provides profile-specific validation middleware that applies different validation rules based on the current pipeline profile (R6.3).
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ProfileSpecificValidationMiddleware" /> class. </remarks>
/// <param name="validatorResolver"> The validator resolver. </param>
/// <param name="options"> The profile validation options. </param>
/// <param name="profileRules"> The collection of profile validation rules. </param>
/// <param name="logger"> The logger. </param>
/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
public sealed partial class ProfileSpecificValidationMiddleware(
	IValidatorResolver validatorResolver,
	IOptions<ProfileValidationOptions> options,
	IEnumerable<IProfileValidationRules> profileRules,
	ILogger<ProfileSpecificValidationMiddleware> logger) : IDispatchMiddleware
{
	private readonly IValidatorResolver
		_validatorResolver = validatorResolver ?? throw new ArgumentNullException(nameof(validatorResolver));

	private readonly IOptions<ProfileValidationOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<ProfileSpecificValidationMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	private readonly Dictionary<string, IProfileValidationRules> _profileRules =
		profileRules?.ToDictionary(static r => r.ProfileName, StringComparer.OrdinalIgnoreCase)
		?? new Dictionary<string, IProfileValidationRules>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets the middleware stage where this middleware should be executed.
	/// </summary>
	/// <value> The current <see cref="Stage" /> value. </value>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

	/// <summary>
	/// Gets the message kinds that this middleware applies to.
	/// </summary>
	/// <value> The current <see cref="ApplicableMessageKinds" /> value. </value>
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <summary>
	/// Invokes the profile-specific validation middleware.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="nextDelegate"> The next delegate in the pipeline. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the validation and pipeline execution. </returns>
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		var profileName = GetCurrentProfile(context);
		var result = ValidateMessageForProfile(message, profileName, context);

		context.ValidationResult(result);

		if (!result.IsValid)
		{
			LogProfileValidationFailed(_logger, message.GetType().Name, profileName, string.Join(", ", result.Errors));

			var problemDetails = new MessageProblemDetails
			{
				Type = ProblemDetailsTypes.Validation,
				Title = "Profile Validation Failed",
				Status = 400,
				Detail = $"Validation failed in profile '{profileName}'",
				Instance = context.CorrelationId ?? Uuid7Extensions.GenerateGuid().ToString(),
				Extensions =
				{
					["errors"] = result.Errors, ["profile"] = profileName, ["validationLevel"] = GetValidationLevel(profileName),
				},
			};

			return MessageResult.Failed(problemDetails);
		}

		LogProfileValidationApplied(_logger, profileName, message.GetType().Name);
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Determines the message kind based on the message type.
	/// </summary>
	/// <param name="message"> The message to analyze. </param>
	/// <returns> The determined message kind. </returns>
	private static MessageKinds DetermineMessageKind(IDispatchMessage message) =>
		message switch
		{
			IDispatchAction => MessageKinds.Action,
			IDispatchEvent => MessageKinds.Event,
			IDispatchDocument => MessageKinds.Document,
			_ => MessageKinds.None,
		};

	/// <summary>
	/// Checks if a field is present and has a valid value in the message.
	/// </summary>
	/// <param name="message"> The message to check. </param>
	/// <param name="fieldName"> The name of the field to check. </param>
	/// <returns> True if the field is present and valid; otherwise, false. </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties'",
		Justification = "Message types are preserved through handler registration and DI container")]
	private static bool IsFieldPresent(IDispatchMessage message, string fieldName)
	{
		var property = message.GetType().GetProperty(fieldName);
		if (property == null)
		{
			return false;
		}

		var value = property.GetValue(message);
		return value switch
		{
			null => false,
			string s => !string.IsNullOrWhiteSpace(s),
			_ => true,
		};
	}

	/// <summary>
	/// Estimates the size of the message in bytes.
	/// </summary>
	/// <param name="message"> The message to estimate. </param>
	/// <returns> The estimated size in bytes, or 0 if estimation fails. </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
		Justification = "JSON serialization used only for size estimation; failure is handled gracefully by returning 0")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "This is a validation method used for development/testing and not in AOT scenarios")]
	private static int EstimateMessageSize(IDispatchMessage message)
	{
		// Simple size estimation based on serialization
		try
		{
			var json = JsonSerializer.Serialize(message);
			return Encoding.UTF8.GetByteCount(json);
		}
		catch
		{
			// If serialization fails, return 0 to skip size check
			return 0;
		}
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.ProfileValidationFailed, LogLevel.Warning,
		"Profile validation failed for {Type} in profile {Profile}: {Errors}")]
	private static partial void LogProfileValidationFailed(
		ILogger logger,
		string type,
		string profile,
		string errors);

	[LoggerMessage(MiddlewareEventId.ProfileValidationApplied, LogLevel.Debug,
		"Applied {Profile} validation rules to {Type}")]
	private static partial void LogProfileValidationApplied(
		ILogger logger,
		string profile,
		string type);

	/// <summary>
	/// Gets the current validation profile from the message context.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The profile name to use for validation. </returns>
	private string GetCurrentProfile(IMessageContext context)
	{
		// Try to get profile from context
		if (context.TryGetValue<object>("DispatchProfile", out var profileValue)
			&& profileValue is string profile
			&& !string.IsNullOrWhiteSpace(profile))
		{
			return profile;
		}

		// Fall back to default profile
		return _options.Value.DefaultProfile ?? "default";
	}

	/// <summary>
	/// Gets the validation level for the specified profile.
	/// </summary>
	/// <param name="profileName"> The profile name. </param>
	/// <returns> The validation level as a string. </returns>
	private string GetValidationLevel(string profileName)
	{
		if (_profileRules.TryGetValue(profileName, out var rules))
		{
			return rules.ValidationLevel.ToString();
		}

		return _options.Value.DefaultValidationLevel.ToString();
	}

	/// <summary>
	/// Validates a message according to the specified profile rules.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <param name="profileName"> The name of the validation profile. </param>
	/// <param name="context"> The message context. </param>
	/// <returns> The validation result. </returns>
	private IValidationResult ValidateMessageForProfile(
		IDispatchMessage message,
		string profileName,
		IMessageContext context)
	{
		// Get profile-specific rules
		if (!_profileRules.TryGetValue(profileName, out var profileRules))
		{
			// Use default validation if no profile-specific rules
			return PerformStandardValidation(message);
		}

		var errors = new List<object>();

		// Apply validation level based on profile
		switch (profileRules.ValidationLevel)
		{
			case ValidationLevel.None:
				// No validation for this profile
				return SerializableValidationResult.Success();

			case ValidationLevel.Basic:
				// Only null checks and basic validation
				if (message == null)
				{
					errors.Add("Message cannot be null");
				}

				break;

			case ValidationLevel.Standard:
				// Standard validation with IValidate and DataAnnotations
				var standardResult = PerformStandardValidation(message);
				if (!standardResult.IsValid)
				{
					errors.AddRange(standardResult.Errors);
				}

				break;

			case ValidationLevel.Strict:
				// Full validation including custom rules
				var strictResult = PerformStrictValidation(message, profileRules, context);
				if (!strictResult.IsValid)
				{
					errors.AddRange(strictResult.Errors);
				}

				break;

			default:
				// Default to standard validation for unknown levels
				var defaultResult = PerformStandardValidation(message);
				if (!defaultResult.IsValid)
				{
					errors.AddRange(defaultResult.Errors);
				}

				break;
		}

		// Apply profile-specific custom validators
		if (message != null)
		{
			foreach (var validator in profileRules.CustomValidators)
			{
				var customResult = validator.Validate(message, context);
				if (!customResult.IsValid)
				{
					errors.AddRange(customResult.Errors);
				}
			}
		}

		// Check required fields for this profile
		if (message != null)
		{
			foreach (var requiredField in profileRules.RequiredFields)
			{
				if (!IsFieldPresent(message, requiredField))
				{
					errors.Add($"Required field '{requiredField}' is missing or empty");
				}
			}

			// Check field constraints
			foreach (var constraint in profileRules.FieldConstraints)
			{
				if (!constraint.IsSatisfied(message))
				{
					errors.Add(constraint.ErrorMessage);
				}
			}
		}

		return errors.Count > 0
			? SerializableValidationResult.Failed([.. errors])
			: SerializableValidationResult.Success();
	}

	/// <summary>
	/// Performs standard validation including IValidate and DataAnnotations.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <returns> The validation result. </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
		Justification =
			"DataAnnotations validation is used for explicit message validation; message types are preserved through handler registration")]
	private IValidationResult PerformStandardValidation(IDispatchMessage message)
	{
		// Try resolver first
		var resolved = _validatorResolver.TryValidate(message);
		if (resolved is not null)
		{
			return resolved;
		}

		var errors = new List<object>();

		// Self-validation
		if (message is IValidate selfValidating)
		{
			var result = selfValidating.Validate();
			if (!result.IsValid)
			{
				errors.AddRange(result.Errors);
			}
		}

		// DataAnnotations validation
		var context = new ValidationContext(message);
		var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
		if (!Validator.TryValidateObject(
			message, context, results, validateAllProperties: true))
		{
			errors.AddRange(results.Select(static r => r.ErrorMessage ?? "Unknown validation error"));
		}

		return errors.Count > 0
			? SerializableValidationResult.Failed([.. errors])
			: SerializableValidationResult.Success();
	}

	/// <summary>
	/// Performs strict validation including custom rules and additional constraints.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <param name="profileRules"> The profile-specific validation rules. </param>
	/// <param name="context"> The message context. </param>
	/// <returns> The validation result. </returns>
	private SerializableValidationResult PerformStrictValidation(
		IDispatchMessage message,
		IProfileValidationRules profileRules,
		IMessageContext context)
	{
		var errors = new List<object>();

		// Start with standard validation
		var standardResult = PerformStandardValidation(message);
		if (!standardResult.IsValid)
		{
			errors.AddRange(standardResult.Errors);
		}

		// Additional strict checks based on message kind
		var messageKind = DetermineMessageKind(message);

		switch (messageKind)
		{
			case MessageKinds.Action:
				// Actions must have correlation ID and timeout
				if (context.CorrelationId == null)
				{
					errors.Add("Actions require a correlation ID in strict mode");
				}

				if (context.Items?.ContainsKey("Timeout") != true)
				{
					errors.Add("Actions require a timeout in strict mode");
				}
				else if (context.Items.TryGetValue("Timeout", out var timeoutObj) && timeoutObj is TimeSpan timeout)
				{
					if (timeout <= TimeSpan.Zero)
					{
						errors.Add("Actions require a positive timeout in strict mode");
					}
				}
				else
				{
					errors.Add("Actions require a valid TimeSpan timeout in strict mode");
				}

				break;

			case MessageKinds.Event:
				// Events must have timestamp and source
				if (context.ReceivedTimestampUtc == default)
				{
					errors.Add("Events require a timestamp in strict mode");
				}

				if (string.IsNullOrWhiteSpace(context.Source))
				{
					errors.Add("Events require a source identifier in strict mode");
				}

				break;

			case MessageKinds.Document:
				// Documents must have version and content type
				if (string.IsNullOrEmpty(context.MessageVersion()))
				{
					errors.Add("Documents require a version in strict mode");
				}

				if (string.IsNullOrEmpty(context.ContentType))
				{
					errors.Add("Documents require a content type in strict mode");
				}

				break;

			case MessageKinds.None:
				// No specific validation for untyped messages
				break;

			case MessageKinds.All:
				// Should not occur in practice, but handle gracefully
				break;

			default:
				break;
		}

		// Check message size limits for strict profile
		var messageSize = EstimateMessageSize(message);
		if (messageSize > profileRules.MaxMessageSize)
		{
			errors.Add(
				$"Message size ({messageSize} bytes) exceeds maximum allowed ({profileRules.MaxMessageSize} bytes) for strict profile");
		}

		return errors.Count > 0
			? SerializableValidationResult.Failed([.. errors])
			: SerializableValidationResult.Success();
	}
}
