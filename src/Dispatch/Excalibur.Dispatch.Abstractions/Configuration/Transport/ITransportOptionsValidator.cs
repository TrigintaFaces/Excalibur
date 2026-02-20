// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Configuration;

/// <summary>
/// Interface for validating transport configuration options.
/// </summary>
/// <remarks>
/// <para>
/// Transport providers implement this interface to provide custom validation logic
/// for their configuration options. Validators are invoked during startup to ensure
/// configuration is correct before the application begins processing messages.
/// </para>
/// <para>
/// Example implementation:
/// <code>
/// public class RabbitMqOptionsValidator : ITransportOptionsValidator
/// {
///     public string TransportName => "RabbitMQ";
///
///     public Task&lt;TransportOptionsValidationResult&gt; ValidateAsync(
///         object options,
///         CancellationToken cancellationToken)
///     {
///         if (options is not RabbitMqOptions mqOptions)
///             return Task.FromResult(TransportOptionsValidationResult.Success());
///
///         var errors = new List&lt;string&gt;();
///         if (string.IsNullOrEmpty(mqOptions.HostName))
///             errors.Add("HostName is required");
///
///         return Task.FromResult(errors.Count == 0
///             ? TransportOptionsValidationResult.Success()
///             : TransportOptionsValidationResult.Failed(errors));
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public interface ITransportOptionsValidator
{
	/// <summary>
	/// Gets the name of the transport this validator applies to.
	/// </summary>
	/// <value> The transport name. </value>
	string TransportName { get; }

	/// <summary>
	/// Validates the transport configuration options.
	/// </summary>
	/// <param name="options"> The transport options to validate. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task containing the validation result. </returns>
	Task<TransportOptionsValidationResult> ValidateAsync(
		object options,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents the result of transport options validation.
/// </summary>
public sealed class TransportOptionsValidationResult
{
	private static readonly TransportOptionsValidationResult SuccessResult =
		new(isValid: true, errors: []);

	private TransportOptionsValidationResult(bool isValid, IReadOnlyList<string> errors)
	{
		IsValid = isValid;
		Errors = errors;
	}

	/// <summary>
	/// Gets a value indicating whether the validation passed.
	/// </summary>
	/// <value> <see langword="true"/> if validation passed; otherwise, <see langword="false"/>. </value>
	public bool IsValid { get; }

	/// <summary>
	/// Gets the collection of validation errors.
	/// </summary>
	/// <value> The validation error messages, empty if validation passed. </value>
	public IReadOnlyList<string> Errors { get; }

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	/// <returns> A successful validation result. </returns>
	public static TransportOptionsValidationResult Success() => SuccessResult;

	/// <summary>
	/// Creates a failed validation result with the specified errors.
	/// </summary>
	/// <param name="errors"> The validation error messages. </param>
	/// <returns> A failed validation result. </returns>
	public static TransportOptionsValidationResult Failed(params string[] errors) =>
		new(isValid: false, errors: errors);

	/// <summary>
	/// Creates a failed validation result with the specified errors.
	/// </summary>
	/// <param name="errors"> The validation error messages. </param>
	/// <returns> A failed validation result. </returns>
	public static TransportOptionsValidationResult Failed(IEnumerable<string> errors) =>
		new(isValid: false, errors: errors.ToList());
}
