// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Aliased rather than imported wholesale: Excalibur.Dispatch.Validation also declares a ValidationError,
// and an unqualified reference in this file must keep resolving to the middleware's own type.
using AbstractionsValidationError = Excalibur.Dispatch.Validation.ValidationError;

// Explicit, because System.ComponentModel.DataAnnotations (imported above for the attribute validator)
// also declares a ValidationException. An unqualified reference binds to THAT one, which carries no
// per-property errors and no ProblemDetails projection -- so the framework's own validation exception
// has to be named outright.
using ValidationException = Excalibur.Dispatch.Exceptions.ValidationException;
using IValidate = Excalibur.Dispatch.Validation.IValidate;
using IValidatorResolver = Excalibur.Dispatch.Validation.IValidatorResolver;
// Common namespace is deprecated - using Messaging.Abstractions instead

namespace Excalibur.Dispatch.Middleware.Validation;

/// <summary>
/// Middleware responsible for validating message content and structure using Data Annotations, FluentValidation, or custom validation logic.
/// </summary>
/// <remarks>
/// This middleware applies validation rules to ensure message integrity before processing. It primarily targets Action messages
/// (commands/queries) as they typically require input validation, while Events are usually trusted internal notifications. The middleware supports:
/// <list type="bullet">
/// <item> Data Annotations validation attributes </item>
/// <item> FluentValidation integration </item>
/// <item> Custom validation logic via IMessageValidationService </item>
/// <item> Contextual validation based on tenant, user, etc. </item>
/// <item> Detailed validation error reporting </item>
/// </list>
/// </remarks>
[AppliesTo(MessageKinds.Action | MessageKinds.Event)]
public sealed partial class ValidationMiddleware : IDispatchMiddleware
{
	private static readonly Func<ILogger, string, bool, IDisposable?> ValidationLogScope =
		LoggerMessage.DefineScope<string, bool>(
			"MessageType:{MessageType} ValidationEnabled:{ValidationEnabled}");

	private static readonly ActivitySource ActivitySource = new(DispatchTelemetryConstants.ActivitySources.ValidationMiddleware, "1.0.0");

	private readonly ValidationOptions _options;

	private readonly IMessageValidationService _validationService;

	private readonly IValidatorResolver _validatorResolver;

	/// <summary>
	/// Stands in for a validation source that reported failure without supplying any error detail.
	/// </summary>
	private const string ValidationRejectedWithoutDetailMessage =
		"The message was rejected by a validator that reported no specific error.";

	private readonly ILogger<ValidationMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationMiddleware"/> class.
	/// Creates a new validation middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for validation. </param>
	/// <param name="validationService"> Service for performing message validation. </param>
	/// <param name="validatorResolver">
	/// Resolves the validator registered for the message type, if any. Supplied by
	/// <c>AddDispatchValidation()</c>, which registers a no-op resolver by default and is replaced by
	/// <c>WithFluentValidation()</c> and friends.
	/// </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public ValidationMiddleware(
		IOptions<ValidationOptions> options,
		IMessageValidationService validationService,
		IValidatorResolver validatorResolver,
		ILogger<ValidationMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(validationService);
		ArgumentNullException.ThrowIfNull(validatorResolver);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_validationService = validationService;
		_validatorResolver = validatorResolver;
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

	/// <inheritdoc />
	/// <remarks>
	/// Validation applies to both Actions (commands/queries) and Events. An event arriving from a transport adapter is inbound and carries
	/// externally-supplied data, so it is validated on the same terms as an Action. Documents are excluded.
	/// <para>
	/// This value must stay in agreement with the <c>AppliesTo</c> attribute on this type. The attribute and this property are read by
	/// different consumers of the applicability decision, so changing one alone makes those consumers disagree about whether validation
	/// applies to the same message.
	/// </para>
	/// </remarks>
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

		using var activity = ActivitySource.StartActivity("ValidationMiddleware.Invoke");

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
				var errorSummary = BuildErrorSummary(validationResult.Errors);
				var exception = new ValidationException(
					string.Concat(
						ErrorConstants.ValidationFailedForMessageType,
						" ",
						message.GetType().Name,
						": ",
						errorSummary));

				// Carry the errors per property, not only flattened into the message. ValidationException
				// projects this dictionary into the RFC 9457 "errors" extension, but only when it is
				// non-empty -- so leaving it unpopulated silently downgrades a field-level validation
				// failure to an opaque string, and the caller cannot tell which field was rejected.
				foreach (var error in validationResult.Errors)
				{
					_ = exception.AddError(error.PropertyName, error.ErrorMessage);
				}

				var detailedErrors = _logger.IsEnabled(LogLevel.Warning)
					? BuildDetailedErrorSummary(validationResult.Errors)
					: string.Empty;

				LogValidationFailed(message.GetType().Name, validationResult.Errors.Count,
					detailedErrors, exception);

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
		IMessageContext context)
	{
		var contextTenantId = context.GetTenantId();
		var contextUserId = context.GetUserId();

		return new(
			message, // Cast to IDispatchMessage
			context,
			string.IsNullOrEmpty(contextTenantId) ? GetPropertyValue(context, "TenantId") : contextTenantId,
			string.IsNullOrEmpty(contextUserId) ? GetPropertyValue(context, "UserId") : contextUserId,
			string.IsNullOrEmpty(context.CorrelationId) ? GetPropertyValue(context, "CorrelationId") : context.CorrelationId);
	}

	/// <summary>
	/// Validates the message using Data Annotations.
	/// </summary>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Data Annotations validation is optional and only used when UseDataAnnotations is true. Message types used with validation are registered at startup and preserved through DI. In AOT builds, validation should be implemented via custom IMessageValidationService.")]
	private static List<ValidationError> ValidateWithDataAnnotations(IDispatchMessage message)
	{
		var errors = new List<ValidationError>();
		var validationContext = new ValidationContext(message);
		var validationResults = new List<ValidationResult>();

		var isValid = Validator.TryValidateObject(message, validationContext, validationResults, validateAllProperties: true);

		if (!isValid)
		{
			for (var i = 0; i < validationResults.Count; i++)
			{
				var validationResult = validationResults[i];
				errors.Add(new ValidationError(
					string.Join(", ", validationResult.MemberNames),
					validationResult.ErrorMessage ?? "Validation error"));
			}
		}

		return errors;
	}

	// Source-generated logging methods
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

		// Every source ACCUMULATES; none of them short-circuits another. A message carrying both a
		// registered validator and [Required] attributes is subject to both, which is how ASP.NET Core
		// model validation and Microsoft.Extensions.Validation behave. Short-circuiting here would mean
		// that registering a single FluentValidation validator silently disabled every annotation on
		// the message -- a failure the author would never see, because it presents as "validation passed".
		if (_options.UseCustomValidation)
		{
			// 1. The registered validator, if the resolver claims this message type.
			var resolved = _validatorResolver.TryValidate(message);
			if (resolved is { IsValid: false })
			{
				AddOrSubstitute(errors, MapResolverErrors(resolved.Errors));
			}

			// 2. Self-validating messages.
			if (message is IValidate validatable)
			{
				var selfValidation = validatable.Validate();
				if (selfValidation is { IsValid: false })
				{
					AddOrSubstitute(errors, MapAbstractionErrors(selfValidation.Errors));
				}
			}
		}

		// 3. Declarative annotations.
		if (_options.UseDataAnnotations)
		{
			var dataAnnotationErrors = ValidateWithDataAnnotations(message);
			errors.AddRange(dataAnnotationErrors);
		}

		// 4. The pluggable validation service, retained so an existing IMessageValidationService keeps working.
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
	/// Appends a source's mapped errors, substituting a placeholder when a source reported failure but
	/// supplied nothing to report.
	/// </summary>
	/// <param name="errors"> The accumulating error list. </param>
	/// <param name="mapped"> The errors mapped from one validation source. </param>
	/// <remarks>
	/// A validation source that says "invalid" while producing no error detail must not be able to let the
	/// message through: <see cref="IValidatorResolver"/> and <see cref="IValidate"/> are public extension
	/// points, so a third-party implementation can return that combination. Dropping it would fail OPEN --
	/// a validator would have rejected the message and the pipeline would dispatch it anyway.
	/// </remarks>
	private static void AddOrSubstitute(List<ValidationError> errors, IEnumerable<ValidationError> mapped)
	{
		var countBefore = errors.Count;
		errors.AddRange(mapped);

		if (errors.Count == countBefore)
		{
			errors.Add(new ValidationError(string.Empty, ValidationRejectedWithoutDetailMessage));
		}
	}

	/// <summary>
	/// Maps the loosely-typed errors an <see cref="IValidatorResolver"/> produces onto the middleware's
	/// error type.
	/// </summary>
	/// <param name="resolverErrors"> The errors reported by the resolver. </param>
	/// <returns> The mapped errors; entries of an unrecognised type are rendered via <c>ToString</c>. </returns>
	/// <remarks>
	/// <see cref="Excalibur.Dispatch.Validation.IValidationResult.Errors"/> is a collection of
	/// <see cref="object"/>, so the element type is not guaranteed. The shipped resolvers all emit
	/// <see cref="AbstractionsValidationError"/>; anything else is still surfaced rather than dropped,
	/// because silently discarding an error would report a message as valid when a validator rejected it.
	/// </remarks>
	private static IEnumerable<ValidationError> MapResolverErrors(IReadOnlyCollection<object> resolverErrors)
	{
		foreach (var error in resolverErrors)
		{
			if (error is AbstractionsValidationError typed)
			{
				yield return new ValidationError(typed.PropertyName ?? string.Empty, typed.Message);
			}
			else if (error is not null)
			{
				yield return new ValidationError(string.Empty, error.ToString() ?? string.Empty);
			}
		}
	}

	/// <summary>
	/// Maps the errors returned by a self-validating message onto the middleware's error type.
	/// </summary>
	/// <param name="abstractionErrors"> The errors reported by <see cref="IValidate.Validate"/>. </param>
	/// <returns> The mapped errors. </returns>
	/// <remarks>
	/// A null <see cref="AbstractionsValidationError.PropertyName"/> becomes an empty key, matching how
	/// ASP.NET Core's ModelState represents an error that belongs to the object rather than to one field.
	/// </remarks>
	private static IEnumerable<ValidationError> MapAbstractionErrors(
		IReadOnlyList<AbstractionsValidationError> abstractionErrors)
	{
		foreach (var error in abstractionErrors)
		{
			if (error is not null)
			{
				yield return new ValidationError(error.PropertyName ?? string.Empty, error.Message);
			}
		}
	}

	/// <summary>
	/// Creates a logging scope with validation context.
	/// </summary>
	/// <exception cref="InvalidOperationException"></exception>
	private IDisposable CreateValidationLoggingScope(IDispatchMessage message)
	{
		return ValidationLogScope(_logger, message.GetType().Name, _options.Enabled)
			?? throw new InvalidOperationException(Resources.ValidationMiddleware_LoggerNotInitialized);
	}

	private static string BuildErrorSummary(IReadOnlyList<ValidationError> errors)
	{
		if (errors.Count == 0)
		{
			return string.Empty;
		}

		if (errors.Count == 1)
		{
			return errors[0].ErrorMessage;
		}

		var totalLength = (errors.Count - 1) * 2;
		for (var i = 0; i < errors.Count; i++)
		{
			totalLength += errors[i].ErrorMessage.Length;
		}

		return string.Create(totalLength, errors, static (span, sourceErrors) =>
		{
			var writeIndex = 0;
			for (var i = 0; i < sourceErrors.Count; i++)
			{
				if (i > 0)
				{
					span[writeIndex++] = ';';
					span[writeIndex++] = ' ';
				}

				var message = sourceErrors[i].ErrorMessage.AsSpan();
				message.CopyTo(span.Slice(writeIndex));
				writeIndex += message.Length;
			}
		});
	}

	private static string BuildDetailedErrorSummary(IReadOnlyList<ValidationError> errors)
	{
		if (errors.Count == 0)
		{
			return string.Empty;
		}

		var totalLength = (errors.Count - 1) * 2;
		for (var i = 0; i < errors.Count; i++)
		{
			var error = errors[i];
			totalLength += error.PropertyName.Length;
			totalLength += error.ErrorMessage.Length;
			totalLength += 2;
		}

		return string.Create(totalLength, errors, static (span, sourceErrors) =>
		{
			var writeIndex = 0;
			for (var i = 0; i < sourceErrors.Count; i++)
			{
				if (i > 0)
				{
					span[writeIndex++] = ';';
					span[writeIndex++] = ' ';
				}

				var error = sourceErrors[i];
				var propertyName = error.PropertyName.AsSpan();
				propertyName.CopyTo(span.Slice(writeIndex));
				writeIndex += propertyName.Length;
				span[writeIndex++] = ':';
				span[writeIndex++] = ' ';
				var message = error.ErrorMessage.AsSpan();
				message.CopyTo(span.Slice(writeIndex));
				writeIndex += message.Length;
			}
		});
	}
}
