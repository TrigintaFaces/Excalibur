// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Text;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Domain.Exceptions;

/// <summary>
/// Represents an exception that occurs when a configuration setting is invalid or missing.
/// </summary>
[Serializable]
public class InvalidConfigurationException : ApiException
{
	private static readonly CompositeFormat SettingMissingFormat =
			CompositeFormat.Parse(Resources.InvalidConfigurationException_SettingMissing);

	/// <summary>
	/// Initializes a new instance of the <see cref="InvalidConfigurationException" /> class with a default error message.
	/// </summary>
	public InvalidConfigurationException()
			: base(Resources.InvalidConfigurationException_DefaultMessage) =>
			Setting = string.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="InvalidConfigurationException" /> class with a specified error message.
	/// </summary>
	/// <param name="message"> The error message describing the exception. </param>
	public InvalidConfigurationException(string message)
		: base(message) =>
		Setting = string.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="InvalidConfigurationException" /> class with a specified error message and an inner exception.
	/// </summary>
	/// <param name="message"> The error message describing the exception. </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	public InvalidConfigurationException(string message, Exception? innerException)
		: base(message, innerException) =>
		Setting = string.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="InvalidConfigurationException" /> class with the name of the problematic setting, an
	/// optional status code, an optional error message, and an optional inner exception.
	/// </summary>
	/// <param name="setting"> The name of the configuration setting that caused the exception. </param>
	/// <param name="statusCode"> The HTTP status code associated with the exception. If not provided, defaults to 500. </param>
	/// <param name="message">
	/// The error message describing the exception. If not provided, a default message is constructed using the <paramref name="setting" />.
	/// </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	public InvalidConfigurationException(string setting, int? statusCode = null, string? message = null, Exception? innerException = null)
			: base(
					statusCode ?? 500,
					message ?? string.Format(CultureInfo.CurrentCulture, SettingMissingFormat, setting),
					innerException)
	{
		ArgumentNullException.ThrowIfNull(setting);

		Setting = setting;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InvalidConfigurationException"/> class with a specified status code, message, and inner exception.
	/// </summary>
	/// <param name="statusCode">The HTTP status code associated with the exception.</param>
	/// <param name="message">The error message describing the exception.</param>
	/// <param name="innerException">The exception that caused the current exception.</param>
	public InvalidConfigurationException(int statusCode, string? message, Exception? innerException)
		: base(statusCode, message, innerException)
	{
		Setting = string.Empty;
	}

	/// <summary>
	/// Gets or sets the name of the configuration setting that caused the exception.
	/// </summary>
	/// <value> The name of the configuration setting that caused the exception. </value>
	public string Setting { get; protected set; }
}
