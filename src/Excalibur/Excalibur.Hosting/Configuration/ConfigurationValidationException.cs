// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.Configuration;

/// <summary>
/// Exception thrown when configuration validation fails.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ConfigurationValidationException" /> class. </remarks>
/// <param name="message"> The error message. </param>
/// <param name="errors"> The validation errors. </param>
/// <param name="innerException"> The inner exception. </param>
public sealed class ConfigurationValidationException(string message, IReadOnlyList<ConfigurationValidationError> errors, Exception? innerException = null)
	: Exception(message, innerException)
{
	public ConfigurationValidationException()
		: this(string.Empty, [], null)
	{
	}

	public ConfigurationValidationException(string? message)
		: this(message ?? string.Empty, [], null)
	{
	}

	public ConfigurationValidationException(string? message, IReadOnlyList<ConfigurationValidationError> errors)
		: this(message ?? string.Empty, errors, null)
	{
	}

	/// <summary>
	/// Gets the validation errors.
	/// </summary>
	/// <value> The validation errors. </value>
	public IReadOnlyList<ConfigurationValidationError> Errors { get; } = errors ?? [];
}
