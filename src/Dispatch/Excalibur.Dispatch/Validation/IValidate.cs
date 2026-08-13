// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Defines a message that can perform self-validation.
/// </summary>
/// <remarks>
/// Messages implementing this interface can provide custom validation logic beyond what declarative validation (attributes,
/// FluentValidation) can handle. This is useful for:
/// <list type="bullet">
/// <item> Complex business rule validation </item>
/// <item> Cross-field validation that requires business logic </item>
/// <item> Validation that depends on external state or services </item>
/// <item> Performance-critical validation scenarios </item>
/// </list>
/// Self-validation is invoked by the validation middleware alongside the other validation sources rather than after them: every applicable
/// source runs and its errors accumulate, so implementing this interface does not suppress the declarative attributes on the same message.
/// Keep validation logic lightweight and avoid external dependencies where possible for better testability and performance.
/// </remarks>
/// <example>
/// <code>
/// public record CreateOrderCommand(decimal Amount, string Currency) : IDispatchAction, IValidate
/// {
/// public ValidationResult Validate()
/// {
/// if (Amount &lt;= 0)
/// return ValidationResult.Failure("Amount must be greater than zero");
///
/// if (string.IsNullOrWhiteSpace(Currency))
/// return ValidationResult.Failure("Currency is required");
///
/// return ValidationResult.Success();
/// }
/// }
/// </code>
/// </example>
public interface IValidate : IDispatchMessage
{
	/// <summary>
	/// Performs custom validation logic and returns the validation result.
	/// </summary>
	/// <returns> A ValidationResult indicating whether validation passed or failed. </returns>
	/// <remarks>
	/// Implement this method to provide message-specific validation logic. The method should be deterministic and avoid side effects.
	/// Consider performance implications as this method may be called frequently. Return ValidationResult.Success() for valid messages or
	/// SerializableValidationResult.Failed() with error details for invalid messages.
	/// </remarks>
	ValidationResult Validate();
}
