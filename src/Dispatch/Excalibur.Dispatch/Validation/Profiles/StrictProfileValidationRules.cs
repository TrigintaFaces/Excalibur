// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.RegularExpressions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Validation rules for the "strict" profile used with Actions requiring maximum validation.
/// </summary>
public partial class StrictProfileValidationRules : IProfileValidationRules
{
	private readonly List<ICustomValidator> _customValidators;
	private readonly List<IFieldConstraint> _fieldConstraints;

	/// <summary>
	/// Initializes a new instance of the <see cref="StrictProfileValidationRules" /> class.
	/// </summary>
	public StrictProfileValidationRules()
	{
		_customValidators =
			[new ActionTimeoutValidator(), new CorrelationIdValidator(), new AuthorizationValidator(), new IdempotencyKeyValidator()];

		_fieldConstraints =
		[
			new NonEmptyStringConstraint("MessageId", "MessageId cannot be empty in strict profile"),
			new GuidFormatConstraint("CorrelationId", "CorrelationId must be a valid GUID in strict profile"),
			new PositiveNumberConstraint("RetryCount", "RetryCount must be non-negative in strict profile"),
			new FutureDateConstraint("ExpiresAt", "ExpiresAt must be in the future for strict profile"),
		];
	}

	/// <summary>
	/// Gets the name of this validation profile.
	/// </summary>
	/// <value>The current <see cref="ProfileName"/> value.</value>
	public string ProfileName => "strict";

	/// <summary>
	/// Gets the validation level for this profile.
	/// </summary>
	/// <value>The current <see cref="ValidationLevel"/> value.</value>
	public ValidationLevel ValidationLevel => ValidationLevel.Strict;

	/// <summary>
	/// Gets the maximum allowed message size in bytes for this profile.
	/// </summary>
	/// <value>The current <see cref="MaxMessageSize"/> value.</value>
	public int MaxMessageSize => 512_000; // 500KB for actions

	/// <summary>
	/// Gets the list of required fields for this profile.
	/// </summary>
	/// <value>The current <see cref="RequiredFields"/> value.</value>
	public IReadOnlyList<string> RequiredFields => new[] { "MessageId", "CorrelationId", "Timestamp", "Source" };

	/// <summary>
	/// Gets the custom validators for this profile.
	/// </summary>
	/// <value>The current <see cref="CustomValidators"/> value.</value>
	public IReadOnlyList<ICustomValidator> CustomValidators => _customValidators;

	/// <summary>
	/// Gets the field constraints for this profile.
	/// </summary>
	/// <value>The current <see cref="FieldConstraints"/> value.</value>
	public IReadOnlyList<IFieldConstraint> FieldConstraints => _fieldConstraints;

	/// <summary>
	/// Validator that ensures actions have valid timeouts.
	/// </summary>
	private sealed class ActionTimeoutValidator : ICustomValidator
	{
		/// <summary>
		/// Validates that actions have appropriate timeout values.
		/// </summary>
		/// <param name="message"> The message to validate. </param>
		/// <param name="context"> The message context. </param>
		/// <returns> The validation result. </returns>
		public IValidationResult Validate(IDispatchMessage message, IMessageContext context)
		{
			if (message is IDispatchAction)
			{
				if (!context.Items?.ContainsKey("Timeout") == true)
				{
					return SerializableValidationResult.Failed("Actions must have a positive timeout in strict profile");
				}

				if (context.Items?.TryGetValue("Timeout", out var timeoutObj) == true && timeoutObj is TimeSpan timeout)
				{
					if (timeout <= TimeSpan.Zero)
					{
						return SerializableValidationResult.Failed("Actions must have a positive timeout in strict profile");
					}

					if (timeout > TimeSpan.FromMinutes(5))
					{
						return SerializableValidationResult.Failed("Action timeout cannot exceed 5 minutes in strict profile");
					}
				}
				else
				{
					return SerializableValidationResult.Failed("Actions must have a valid TimeSpan timeout in strict profile");
				}
			}

			return SerializableValidationResult.Success();
		}
	}

	/// <summary>
	/// Validator that ensures messages have valid correlation IDs.
	/// </summary>
	private sealed class CorrelationIdValidator : ICustomValidator
	{
		/// <summary>
		/// Validates that the correlation ID is present and valid.
		/// </summary>
		/// <param name="message"> The message to validate. </param>
		/// <param name="context"> The message context. </param>
		/// <returns> The validation result. </returns>
		public IValidationResult Validate(IDispatchMessage message, IMessageContext context)
		{
			if (string.IsNullOrWhiteSpace(context.CorrelationId) || !Guid.TryParse(context.CorrelationId, out var correlationId) ||
				correlationId == Guid.Empty)
			{
				return SerializableValidationResult.Failed("CorrelationId is required and must be non-empty in strict profile");
			}

			return SerializableValidationResult.Success();
		}
	}

	/// <summary>
	/// Validator that ensures proper authorization context for actions.
	/// </summary>
	private sealed class AuthorizationValidator : ICustomValidator
	{
		/// <summary>
		/// Validates that actions have proper authorization context.
		/// </summary>
		/// <param name="message"> The message to validate. </param>
		/// <param name="context"> The message context. </param>
		/// <returns> The validation result. </returns>
		public IValidationResult Validate(IDispatchMessage message, IMessageContext context)
		{
			if (message is IDispatchAction)
			{
				// Check for authorization context
				if (string.IsNullOrEmpty(context.UserId) && !context.Items?.ContainsKey("SystemInitiated") == true)
				{
					return SerializableValidationResult.Failed("Actions require user context or SystemInitiated flag in strict profile");
				}

				// Validate user claims if present
				if (!string.IsNullOrEmpty(context.UserId) && !IsUserAuthenticated(context))
				{
					return SerializableValidationResult.Failed("User must be authenticated for Actions in strict profile");
				}
			}

			return SerializableValidationResult.Success();
		}

		/// <summary>
		/// Checks if the user is authenticated based on context items.
		/// </summary>
		/// <param name="context"> The message context. </param>
		/// <returns> True if the user is authenticated; otherwise, false. </returns>
		private static bool IsUserAuthenticated(IMessageContext context)
		{
			// Check for authentication flags in context items
			if (context.Items?.TryGetValue("UserAuthenticated", out var authObj) == true && authObj is bool isAuthenticated)
			{
				return isAuthenticated;
			}

			// Check for authentication claims or tokens
			if (context.Items?.ContainsKey("AuthenticationToken") == true ||
				context.Items?.ContainsKey("UserClaims") == true)
			{
				return true;
			}

			// Default to false if no authentication evidence found
			return false;
		}
	}

	/// <summary>
	/// Validator that ensures actions have valid idempotency keys.
	/// </summary>
	private sealed partial class IdempotencyKeyValidator : ICustomValidator
	{
		/// <summary>
		/// Validates that actions have proper idempotency keys.
		/// </summary>
		/// <param name="message"> The message to validate. </param>
		/// <param name="context"> The message context. </param>
		/// <returns> The validation result. </returns>
		public IValidationResult Validate(IDispatchMessage message, IMessageContext context)
		{
			if (message is IDispatchAction)
			{
				// Actions should have an idempotency key
				if (!context.Items?.ContainsKey("IdempotencyKey") == true)
				{
					return SerializableValidationResult.Failed("Actions require an IdempotencyKey in strict profile");
				}

				var key = context.Items?["IdempotencyKey"]?.ToString();
				if (string.IsNullOrWhiteSpace(key))
				{
					return SerializableValidationResult.Failed("IdempotencyKey cannot be empty in strict profile");
				}

				// Validate key format (UUID or similar)
				if (!IsValidIdempotencyKey(key))
				{
					return SerializableValidationResult.Failed(
						"IdempotencyKey must be a valid UUID or structured identifier in strict profile");
				}
			}

			return SerializableValidationResult.Success();
		}

		/// <summary>
		/// Validates the format of an idempotency key.
		/// </summary>
		/// <param name="key"> The key to validate. </param>
		/// <returns> True if the key is valid; otherwise, false. </returns>
		private static bool IsValidIdempotencyKey(string key) =>

			// Accept GUID format or structured key like "order-12345-create"
			Guid.TryParse(key, out _) || IdempotencyKeyRegex().IsMatch(key);

		[GeneratedRegex(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled)]
		private static partial Regex IdempotencyKeyRegex();
	}
}
