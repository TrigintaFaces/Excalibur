// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Default implementation of the validation service that provides message validation capabilities.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="DefaultValidationService" /> class. </remarks>
/// <param name="options"> The validation options. </param>
/// <param name="logger"> The logger instance. </param>
/// <param name="schemaValidator"> The optional schema validator strategy; when absent and schema validation is enabled, validation surfaces an error rather than silently passing. </param>
internal sealed partial class DefaultValidationService(
	IOptions<ValidationOptions> options,
	ILogger<DefaultValidationService> logger,
	IMessageSchemaValidator? schemaValidator = null)
	: IValidationService
{
	private readonly ValidationOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<DefaultValidationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly IMessageSchemaValidator? _schemaValidator = schemaValidator;

	/// <summary>
	/// Validates a message asynchronously.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <param name="context"> The validation context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous validation operation. </returns>
	public async ValueTask<ValidationResult> ValidateAsync(object message, MessageValidationContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		if (!_options.Enabled)
		{
			return ValidationResult.Success();
		}

		using var timeoutCts = new CancellationTokenSource(_options.ValidationTimeout);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		try
		{
			var errors = new List<ValidationError>();

			// Perform validation based on configuration
			if (_options.ValidateContracts)
			{
				await ValidateContractsAsync(message, context, errors, linkedCts.Token).ConfigureAwait(false);
			}

			if (_options.ValidateSchemas && errors.Count < _options.MaxErrors)
			{
				await ValidateSchemasAsync(message, context, errors, linkedCts.Token).ConfigureAwait(false);
			}

			// Perform data annotations validation
			if (errors.Count < _options.MaxErrors)
			{
				ValidateDataAnnotations(message, errors);
			}

			if (errors.Count > 0)
			{
				if (_options.IncludeDetailedErrors)
				{
					LogValidationFailedWithDetails(context.MessageType.Name, errors.Count, context.MessageId);
				}
				else
				{
					LogValidationFailed(context.MessageType.Name, context.MessageId);
				}

				return ValidationResult.Failure(errors.ToArray());
			}

			LogValidationSucceeded(context.MessageType.Name, context.MessageId);

			return ValidationResult.Success();
		}
		catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
		{
			LogValidationTimedOut(context.MessageType.Name, context.MessageId, _options.ValidationTimeout.TotalMilliseconds);

			return ValidationResult.Failure("Validation timeout exceeded");
		}
		catch (Exception ex)
		{
			LogValidationError(ex, context.MessageType.Name, context.MessageId);

			return ValidationResult.Failure($"Validation error: {ex.Message}");
		}
	}

	/// <summary>
	/// Validates message schemas asynchronously.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <param name="context"> The validation context. </param>
	/// <param name="errors"> The collection to add validation errors to. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous validation operation. </returns>
	private async ValueTask ValidateSchemasAsync(object message, MessageValidationContext context, List<ValidationError> errors,
		CancellationToken cancellationToken)
	{
		if (_schemaValidator is null)
		{
			// Schema validation is enabled (ValidationOptions.ValidateSchemas == true) but no
			// IMessageSchemaValidator is registered. Fail loud rather than silently pass: surface a
			// validation error so the misconfiguration is visible. (A startup ValidateOnStart guard should
			// catch this earlier; this is the defensive runtime backstop so the flag is never a silent no-op.)
			errors.Add(new ValidationError(
				context.MessageType.Name,
				"Schema validation is enabled (ValidateSchemas = true) but no IMessageSchemaValidator is registered."));

			return;
		}

		await _schemaValidator.ValidateAsync(message, context, errors, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Validates message contracts asynchronously.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <param name="context"> The validation context. </param>
	/// <param name="errors"> The collection to add validation errors to. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous validation operation. </returns>
	[SuppressMessage("Style", "RCS1163:Unused parameter", Justification = "Parameters reserved for future implementation")]
	[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Parameters reserved for future implementation")]
	private async ValueTask ValidateContractsAsync(object message, MessageValidationContext context, List<ValidationError> errors,
		CancellationToken cancellationToken)
	{
		// Contract validation logic would go here This could include checking message type compatibility, version checks, etc.
		await ValueTask.CompletedTask.ConfigureAwait(false);

		// Example contract validation. When FailFast is enabled, stop accumulating errors as soon as the
		// MaxErrors threshold is reached so the caller is not charged for validation work past the limit.
		if (context.MessageType == null)
		{
			errors.Add(new ValidationError("MessageType", ErrorMessages.MessageTypeIsRequired));

			if (errors.Count >= _options.MaxErrors && _options.FailFast)
			{
				return;
			}
		}

		if (string.IsNullOrEmpty(context.MessageId))
		{
			errors.Add(new ValidationError("MessageId", ErrorMessages.MessageIdIsRequired));

			if (errors.Count >= _options.MaxErrors && _options.FailFast)
			{
				return;
			}
		}
	}

	// Schema validation would be implemented based on specific requirements// For now, this is a placeholder for future schema validation functionality

	/// <summary>
	/// Validates the message using data annotations.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <param name="errors"> The collection to add validation errors to. </param>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
		Justification =
			"DataAnnotations validation is used for explicit message validation; message types are preserved through handler registration")]
	private void ValidateDataAnnotations(object message, List<ValidationError> errors)
	{
		var validationContext = new ValidationContext(message);
		var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

		if (!Validator.TryValidateObject(message, validationContext, validationResults, validateAllProperties: true))
		{
			foreach (var validationResult in validationResults)
			{
				var propertyName = GetFirstMemberName(validationResult.MemberNames);
				var errorMessage = validationResult.ErrorMessage ?? "Validation failed";

				errors.Add(new ValidationError(propertyName ?? string.Empty, errorMessage));

				if (errors.Count >= _options.MaxErrors && _options.FailFast)
				{
					break;
				}
			}
		}
	}

	private static string? GetFirstMemberName(IEnumerable<string> memberNames)
	{
		if (memberNames is IList<string> list)
		{
			return list.Count > 0 ? list[0] : null;
		}

		using var enumerator = memberNames.GetEnumerator();
		return enumerator.MoveNext() ? enumerator.Current : null;
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.ValidationFailedWithDetails, LogLevel.Warning,
		"Message validation failed for {MessageType} with {ErrorCount} errors. MessageId: {MessageId}")]
	private partial void LogValidationFailedWithDetails(string messageType, int errorCount, string messageId);

	[LoggerMessage(MiddlewareEventId.ValidationFailed, LogLevel.Warning,
		"Message validation failed for {MessageType}. MessageId: {MessageId}")]
	private partial void LogValidationFailed(string messageType, string messageId);

	[LoggerMessage(MiddlewareEventId.ValidationPassed, LogLevel.Debug,
		"Message validation succeeded for {MessageType}. MessageId: {MessageId}")]
	private partial void LogValidationSucceeded(string messageType, string messageId);

	[LoggerMessage(MiddlewareEventId.ValidationTimedOut, LogLevel.Warning,
		"Message validation timed out for {MessageType}. MessageId: {MessageId}, Timeout: {Timeout}ms")]
	private partial void LogValidationTimedOut(string messageType, string messageId, double timeout);

	[LoggerMessage(MiddlewareEventId.ValidationServiceError, LogLevel.Error,
		"Unexpected error during message validation for {MessageType}. MessageId: {MessageId}")]
	private partial void LogValidationError(Exception ex, string messageType, string messageId);
}
