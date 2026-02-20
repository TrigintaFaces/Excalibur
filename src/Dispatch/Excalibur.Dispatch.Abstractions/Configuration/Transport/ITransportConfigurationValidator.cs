// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Configuration;

/// <summary>
/// Validates transport configurations before startup.
/// </summary>
/// <remarks>
/// <para>
/// The validator checks transport registrations for common configuration errors
/// before the transport lifecycle begins. This enables early failure with clear
/// error messages rather than runtime failures.
/// </para>
/// <para>
/// Validation rules include:
/// <list type="bullet">
///   <item>Transport name uniqueness (no duplicates)</item>
///   <item>Required options validation (connection strings, cron expressions)</item>
///   <item>Format validation (connection string format, cron syntax)</item>
/// </list>
/// </para>
/// </remarks>
public interface ITransportConfigurationValidator
{
	/// <summary>
	/// Validates the provided transport registrations.
	/// </summary>
	/// <param name="registrations">The transport registrations to validate.</param>
	/// <returns>A validation result indicating success or containing error details.</returns>
	TransportValidationResult Validate(IEnumerable<TransportRegistrationInfo> registrations);
}

/// <summary>
/// Information about a transport registration for validation.
/// </summary>
/// <param name="Name">The transport name.</param>
/// <param name="TransportType">The transport type identifier (e.g., "kafka", "rabbitmq").</param>
/// <param name="Options">The transport options dictionary.</param>
public sealed record TransportRegistrationInfo(
	string Name,
	string TransportType,
	IReadOnlyDictionary<string, object>? Options = null);

/// <summary>
/// The result of transport configuration validation.
/// </summary>
public sealed class TransportValidationResult
{
	/// <summary>
	/// Gets a value indicating whether validation succeeded.
	/// </summary>
	public bool IsValid { get; init; }

	/// <summary>
	/// Gets the validation errors, if any.
	/// </summary>
	public IReadOnlyList<TransportValidationError> Errors { get; init; } = [];

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	/// <returns>A validation result indicating success.</returns>
	public static TransportValidationResult Success() => new() { IsValid = true };

	/// <summary>
	/// Creates a failed validation result with the specified errors.
	/// </summary>
	/// <param name="errors">The validation errors.</param>
	/// <returns>A validation result containing the errors.</returns>
	public static TransportValidationResult Failure(IEnumerable<TransportValidationError> errors) =>
		new() { IsValid = false, Errors = errors.ToList() };

	/// <summary>
	/// Creates a failed validation result with a single error.
	/// </summary>
	/// <param name="transportName">The transport name with the error.</param>
	/// <param name="property">The property that failed validation.</param>
	/// <param name="message">The error message.</param>
	/// <returns>A validation result containing the error.</returns>
	public static TransportValidationResult Failure(string transportName, string property, string message) =>
		Failure([new TransportValidationError(transportName, property, message)]);
}

/// <summary>
/// Represents a validation error for a transport configuration.
/// </summary>
/// <param name="TransportName">The name of the transport with the error.</param>
/// <param name="Property">The property that failed validation.</param>
/// <param name="Message">The error message describing the validation failure.</param>
public sealed record TransportValidationError(
	string TransportName,
	string Property,
	string Message);
