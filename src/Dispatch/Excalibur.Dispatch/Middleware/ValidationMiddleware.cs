// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Common namespace is deprecated - using Messaging.Abstractions instead
using IMessageContext = Excalibur.Dispatch.Abstractions.IMessageContext;
using IMessageResult = Excalibur.Dispatch.Abstractions.IMessageResult;
using MessageKinds = Excalibur.Dispatch.Abstractions.MessageKinds;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware responsible for validating message content and structure using Data Annotations, FluentValidation, or custom validation logic.
/// </summary>
/// <remarks>
/// This middleware applies validation rules to ensure message integrity before processing. It primarily targets Action messages
/// (commands/queries) as they typically require input validation, while Events are usually trusted internal notifications. The middleware supports:
/// <list type="bullet">
/// <item> Data Annotations validation attributes </item>
/// <item> FluentValidation integration </item>
/// <item> Custom validation logic via IValidationService </item>
/// <item> Contextual validation based on tenant, user, etc. </item>
/// <item> Detailed validation error reporting </item>
/// </list>
/// </remarks>
[AppliesTo(MessageKinds.Action)]
public sealed partial class ValidationMiddleware : IDispatchMiddleware
{
	private readonly ValidationOptions _options;

	private readonly IValidationService _validationService;

	private readonly ILogger<ValidationMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationMiddleware"/> class.
	/// Creates a new validation middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for validation. </param>
	/// <param name="validationService"> Service for performing message validation. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public ValidationMiddleware(
		IOptions<ValidationOptions> options,
		IValidationService validationService,
		ILogger<ValidationMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(validationService);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_validationService = validationService;
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

	/// <inheritdoc />
	/// <remarks>
	/// Validation typically applies to Actions (commands/queries) that represent user input requiring validation, rather than Events which
	/// are internal system notifications.
	/// </remarks>
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

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

		// Skip validation if disabled
		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Extract validation context
		var validationContext = CreateValidationContext(message, context);

		// Set up logging scope
		using var logScope = CreateValidationLoggingScope(message);

		// Set up OpenTelemetry activity tags
		SetValidationActivityTags(message);

		LogValidatingMessage(message.GetType().Name);

		try
		{
			// Perform validation
			var validationResult = await ValidateMessageAsync(message, validationContext, cancellationToken)
				.ConfigureAwait(false);

			if (!validationResult.IsValid)
			{
				var errorSummary = string.Join(
					"; ",
					validationResult.Errors.Select(static e => e.ErrorMessage));
				var exception = new ValidationException(
					string.Concat(
						ErrorConstants.ValidationFailedForMessageType,
						" ",
						message.GetType().Name,
						": ",
						errorSummary));

				LogValidationFailed(message.GetType().Name, validationResult.Errors.Count,
					string.Join("; ", validationResult.Errors.Select(static e => $"{e.PropertyName}: {e.ErrorMessage}")), exception);

				throw exception;
			}

			LogValidationSucceeded(message.GetType().Name);

			// Continue pipeline execution
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			return result;
		}
		catch (Exception ex) when (ex is not ValidationException)
		{
			LogValidationException(message.GetType().Name, ex);
			throw;
		}
	}

	/// <summary>
	/// Gets a property value from the message context.
	/// </summary>
	private static string? GetPropertyValue(IMessageContext context, string propertyName)
	{
		// Use GetItem instead of Properties
		var value = context.GetItem<object>(propertyName);
		return value?.ToString();
	}

	/// <summary>
	/// Sets OpenTelemetry activity tags for validation tracing.
	/// </summary>
	private static void SetValidationActivityTags(IDispatchMessage message)
	{
		var activity = Activity.Current;
		if (activity == null)
		{
			return;
		}

		_ = activity.SetTag("validation.message_type", message.GetType().Name);
		_ = activity.SetTag("validation.enabled", value: true);
	}

	/// <summary>
	/// Creates a validation context with additional contextual information.
	/// </summary>
	private static MessageValidationContext CreateValidationContext(
		IDispatchMessage message,
		IMessageContext context) =>
		new(
			message, // Cast to IDispatchMessage
			context,
			GetPropertyValue(context, "TenantId"),
			GetPropertyValue(context, "UserId"),
			GetPropertyValue(context, "CorrelationId"));

	/// <summary>
	/// Validates the message using Data Annotations.
	/// </summary>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Data Annotations validation is optional and only used when UseDataAnnotations is true. Message types used with validation are registered at startup and preserved through DI. In AOT builds, validation should be implemented via custom IValidationService.")]
	private static List<ValidationError> ValidateWithDataAnnotations(IDispatchMessage message)
	{
		var errors = new List<ValidationError>();
		var validationContext = new ValidationContext(message);
		var validationResults = new List<ValidationResult>();

		var isValid = Validator.TryValidateObject(message, validationContext, validationResults, validateAllProperties: true);

		if (!isValid)
		{
			errors.AddRange(validationResults.Select(static vr => new ValidationError(
				string.Join(", ", vr.MemberNames),
				vr.ErrorMessage ?? "Validation error")));
		}

		return errors;
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.ValidationMiddlewareExecuting, LogLevel.Debug,
		"Validating message {MessageType}")]
	private partial void LogValidatingMessage(string messageType);

	[LoggerMessage(MiddlewareEventId.ValidationFailed, LogLevel.Warning,
		"Validation failed for message {MessageType} with {ErrorCount} errors: {Errors}")]
	private partial void LogValidationFailed(string messageType, int errorCount, string errors, Exception ex);

	[LoggerMessage(MiddlewareEventId.ValidationPassed, LogLevel.Debug,
		"Validation succeeded for message {MessageType}")]
	private partial void LogValidationSucceeded(string messageType);

	[LoggerMessage(MiddlewareEventId.ValidationErrorDetails, LogLevel.Error,
		"Exception occurred during message validation for {MessageType}")]
	private partial void LogValidationException(string messageType, Exception ex);

	/// <summary>
	/// Validates the message using the configured validation strategy.
	/// </summary>
	private async Task<MessageValidationResult> ValidateMessageAsync(
		IDispatchMessage message,
		MessageValidationContext validationContext,
		CancellationToken cancellationToken)
	{
		var errors = new List<ValidationError>();

		// Perform Data Annotations validation if enabled
		if (_options.UseDataAnnotations)
		{
			var dataAnnotationErrors = ValidateWithDataAnnotations(message);
			errors.AddRange(dataAnnotationErrors);
		}

		// Perform custom validation using validation service
		if (_options.UseCustomValidation)
		{
			var customValidationResult = await _validationService
				.ValidateAsync(message, validationContext, cancellationToken)
				.ConfigureAwait(false);
			errors.AddRange(customValidationResult.Errors);
		}

		// Stop on first error if configured
		if (_options.StopOnFirstError && errors.Count > 0)
		{
			errors = [errors[0]];
		}

		return new MessageValidationResult(errors.Count == 0, errors);
	}

	/// <summary>
	/// Creates a logging scope with validation context.
	/// </summary>
	/// <exception cref="InvalidOperationException"></exception>
	private IDisposable CreateValidationLoggingScope(IDispatchMessage message)
	{
		var scopeProperties = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["MessageType"] = message.GetType().Name,
			["ValidationEnabled"] = _options.Enabled,
		};

		return _logger?.BeginScope(scopeProperties)
			?? throw new InvalidOperationException(Resources.ValidationMiddleware_LoggerNotInitialized);
	}
}
