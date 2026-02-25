// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Validation.Context;

/// <summary>
/// Result of context validation containing details about any issues found.
/// </summary>
public sealed class ContextValidationResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the context validation was successful.
	/// </summary>
	/// <value> The current <see cref="IsValid" /> value. </value>
	public bool IsValid { get; set; } = true;

	/// <summary>
	/// Gets or sets the reason for validation failure.
	/// </summary>
	/// <value> The current <see cref="FailureReason" /> value. </value>
	public string FailureReason { get; set; } = string.Empty;

	/// <summary>
	/// Gets the list of missing required fields.
	/// </summary>
	/// <value> The current <see cref="MissingFields" /> value. </value>
	public IList<string> MissingFields { get; init; } = [];

	/// <summary>
	/// Gets the list of fields detected as corrupted.
	/// </summary>
	/// <value> The current <see cref="CorruptedFields" /> value. </value>
	public IList<string> CorruptedFields { get; init; } = [];

	/// <summary>
	/// Gets additional validation details.
	/// </summary>
	/// <value>
	/// Additional validation details.
	/// </value>
	public IDictionary<string, object?> Details { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the timestamp when validation was performed.
	/// </summary>
	/// <value> The current <see cref="ValidationTimestamp" /> value. </value>
	public DateTimeOffset ValidationTimestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the severity of the validation issue.
	/// </summary>
	/// <value> The current <see cref="Severity" /> value. </value>
	public ValidationSeverity Severity { get; set; } = ValidationSeverity.Info;

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	/// <returns> A successful validation result. </returns>
	public static ContextValidationResult Success() =>
		new() { IsValid = true, Severity = ValidationSeverity.Info };

	/// <summary>
	/// Creates a failed validation result.
	/// </summary>
	/// <param name="reason"> The reason for failure. </param>
	/// <param name="severity"> The severity of the issue. </param>
	/// <returns> A failed validation result. </returns>
	public static ContextValidationResult Failure(string reason, ValidationSeverity severity = ValidationSeverity.Error) =>
		new() { IsValid = false, FailureReason = reason, Severity = severity };

	/// <summary>
	/// Creates a failed validation result with field details.
	/// </summary>
	/// <param name="reason"> The reason for failure. </param>
	/// <param name="missingFields"> List of missing fields. </param>
	/// <param name="corruptedFields"> List of corrupted fields. </param>
	/// <param name="severity"> The severity of the issue. </param>
	/// <returns> A failed validation result with field details. </returns>
	public static ContextValidationResult FailureWithFields(
		string reason,
		IEnumerable<string>? missingFields = null,
		IEnumerable<string>? corruptedFields = null,
		ValidationSeverity severity = ValidationSeverity.Error) =>
		new()
		{
			IsValid = false,
			FailureReason = reason,
			MissingFields = missingFields?.ToList() ?? [],
			CorruptedFields = corruptedFields?.ToList() ?? [],
			Severity = severity,
		};
}
