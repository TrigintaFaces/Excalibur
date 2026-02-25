// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Exception thrown when configuration-related errors occur.
/// </summary>
[Serializable]
public sealed class ConfigurationException : DispatchException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConfigurationException" /> class.
	/// </summary>
	public ConfigurationException()
		: base(ErrorCodes.ConfigurationInvalid, ErrorMessages.ConfigurationErrorOccurred)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfigurationException" /> class with a specified error message.
	/// </summary>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	public ConfigurationException(string message)
		: base(ErrorCodes.ConfigurationInvalid, message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfigurationException" /> class with a specified error message and a reference to the
	/// inner exception.
	/// </summary>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	/// <param name="innerException"> The exception that is the cause of the current exception. </param>
	public ConfigurationException(string message, Exception innerException)
		: base(ErrorCodes.ConfigurationInvalid, message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfigurationException" /> class with an explicit error code.
	/// </summary>
	/// <param name="errorCode">The error code to associate with the exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public ConfigurationException(string errorCode, string message) : base(errorCode, message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfigurationException" /> class with an explicit error code and inner exception.
	/// </summary>
	/// <param name="errorCode">The error code to associate with the exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public ConfigurationException(string errorCode, string message, Exception? innerException) : base(errorCode, message, innerException)
	{
	}

	/// <summary>
	/// Creates a configuration missing exception.
	/// </summary>
	/// <param name="configKey"> The configuration key that is missing. </param>
	/// <returns> A new ConfigurationException instance. </returns>
	public static ConfigurationException Missing(string configKey) =>
		new ConfigurationException($"Required configuration '{configKey}' is missing.")
			.WithContext("configKey", configKey)
			.WithSuggestedAction($"Add the '{configKey}' configuration to your appsettings.json or environment variables.")
			.WithStatusCode(500) as ConfigurationException ?? new ConfigurationException();

	/// <summary>
	/// Creates a configuration invalid exception.
	/// </summary>
	/// <param name="configKey"> The configuration key that has an invalid value. </param>
	/// <param name="value"> The invalid value. </param>
	/// <param name="reason"> The reason why the value is invalid. </param>
	/// <returns> A new ConfigurationException instance. </returns>
	public static ConfigurationException Invalid(string configKey, object? value, string reason) =>
		new ConfigurationException($"Configuration '{configKey}' has invalid value: {reason}")
			.WithContext("configKey", configKey)
			.WithContext("invalidValue", value)
			.WithContext("reason", reason)
			.WithSuggestedAction($"Check the value of '{configKey}' configuration. {reason}")
			.WithStatusCode(500) as ConfigurationException ?? new ConfigurationException();

	/// <summary>
	/// Creates a configuration section not found exception.
	/// </summary>
	/// <param name="sectionName"> The name of the configuration section that was not found. </param>
	/// <returns> A new ConfigurationException instance. </returns>
	public static ConfigurationException SectionNotFound(string sectionName)
	{
		var ex = new ConfigurationException($"Configuration section '{sectionName}' was not found.")
		{
			Data = { ["ErrorCode"] = ErrorCodes.ConfigurationSectionNotFound },
		};
		return ex.WithContext("sectionName", sectionName)
			.WithSuggestedAction($"Add the '{sectionName}' section to your configuration file.")
			.WithStatusCode(500) as ConfigurationException ?? new ConfigurationException();
	}
}
