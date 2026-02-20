// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Configuration;

/// <summary>
/// Base class for configuration validators providing common validation logic.
/// </summary>
public abstract class ConfigurationValidatorBase : IConfigurationValidator
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConfigurationValidatorBase" /> class.
	/// </summary>
	/// <param name="configurationName"> The name of the configuration section or component. </param>
	/// <param name="priority"> The priority of this validator. </param>
	protected ConfigurationValidatorBase(string configurationName, int priority = 100)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(configurationName);

		ConfigurationName = configurationName;
		Priority = priority;
	}

	/// <inheritdoc />
	public string ConfigurationName { get; }

	/// <inheritdoc />
	public int Priority { get; }

	/// <inheritdoc />
	public abstract Task<ConfigurationValidationResult> ValidateAsync(
		IConfiguration configuration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates that a configuration value exists and is not empty.
	/// </summary>
	/// <param name="configuration"> The configuration to validate. </param>
	/// <param name="key"> The configuration key. </param>
	/// <param name="errors"> The list to add errors Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="isRequired"> Whether the value is required. </param>
	/// <returns> The configuration value if valid, null otherwise. </returns>
	protected static string? ValidateRequired(
		IConfiguration configuration,
		string key,
		ICollection<ConfigurationValidationError> errors,
		bool isRequired = true)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(errors);

		var value = configuration[key];

		if (string.IsNullOrWhiteSpace(value) && isRequired)
		{
			errors.Add(new ConfigurationValidationError(
				"Configuration value is missing or empty",
				key,
				value: null,
				$"Set the '{key}' configuration value in appsettings.json or environment variables"));
			return null;
		}

		return value;
	}

	/// <summary>
	/// Validates that a configuration value is within a numeric range.
	/// </summary>
	/// <param name="configuration"> The configuration to validate. </param>
	/// <param name="key"> The configuration key. </param>
	/// <param name="min"> The minimum value (inclusive). </param>
	/// <param name="max"> The maximum value (inclusive). </param>
	/// <param name="errors"> The list to add errors Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="defaultValue"> The default value if not specified. </param>
	/// <returns> The parsed value if valid, defaultValue otherwise. </returns>
	protected static int ValidateIntRange(
		IConfiguration configuration,
		string key,
		int min,
		int max,
		ICollection<ConfigurationValidationError> errors,
		int? defaultValue = null)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(errors);

		var value = configuration[key];

		if (string.IsNullOrWhiteSpace(value))
		{
			if (defaultValue.HasValue)
			{
				return defaultValue.Value;
			}

			errors.Add(new ConfigurationValidationError(
				"Configuration value is missing",
				key,
				value: null,
				$"Set the '{key}' configuration value between {min.ToString(CultureInfo.InvariantCulture)} and {max.ToString(CultureInfo.InvariantCulture)}"));
			return min;
		}

		if (!int.TryParse(value, out var intValue))
		{
			errors.Add(new ConfigurationValidationError(
				"Configuration value is not a valid integer",
				key,
				value,
				$"Set the '{key}' configuration value to a valid integer between {min.ToString(CultureInfo.InvariantCulture)} and {max.ToString(CultureInfo.InvariantCulture)}"));
			return defaultValue ?? min;
		}

		if (intValue < min || intValue > max)
		{
			errors.Add(new ConfigurationValidationError(
				$"Configuration value {intValue.ToString(CultureInfo.InvariantCulture)} is outside the valid range [{min.ToString(CultureInfo.InvariantCulture)}, {max.ToString(CultureInfo.InvariantCulture)}]",
				key,
				intValue,
				$"Set the '{key}' configuration value between {min.ToString(CultureInfo.InvariantCulture)} and {max.ToString(CultureInfo.InvariantCulture)}"));
			return defaultValue ?? min;
		}

		return intValue;
	}

	/// <summary>
	/// Validates that a configuration value is a valid TimeSpan.
	/// </summary>
	/// <param name="configuration"> The configuration to validate. </param>
	/// <param name="key"> The configuration key. </param>
	/// <param name="minValue"> The minimum TimeSpan value. </param>
	/// <param name="maxValue"> The maximum TimeSpan value. </param>
	/// <param name="errors"> The list to add errors Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="defaultValue"> The default value if not specified. </param>
	/// <returns> The parsed TimeSpan if valid, defaultValue otherwise. </returns>
	protected static TimeSpan ValidateTimeSpan(
		IConfiguration configuration,
		string key,
		TimeSpan? minValue,
		TimeSpan? maxValue,
		ICollection<ConfigurationValidationError> errors,
		TimeSpan? defaultValue = null)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(errors);

		var value = configuration[key];

		if (string.IsNullOrWhiteSpace(value))
		{
			if (defaultValue.HasValue)
			{
				return defaultValue.Value;
			}

			errors.Add(new ConfigurationValidationError(
				"Configuration value is missing",
				key,
				value: null,
				$"Set the '{key}' configuration value to a valid TimeSpan (e.g., '00:30:00' for 30 minutes)"));
			return TimeSpan.Zero;
		}

		if (!TimeSpan.TryParse(value, out var timeSpan))
		{
			errors.Add(new ConfigurationValidationError(
				"Configuration value is not a valid TimeSpan",
				key,
				value,
				$"Set the '{key}' configuration value to a valid TimeSpan format (e.g., '00:30:00' for 30 minutes)"));
			return defaultValue ?? TimeSpan.Zero;
		}

		if (minValue.HasValue && timeSpan < minValue.Value)
		{
			errors.Add(new ConfigurationValidationError(
				$"Configuration value {timeSpan} is less than the minimum allowed value {minValue.Value}",
				key,
				timeSpan,
				$"Set the '{key}' configuration value to at least {minValue.Value}"));
			return defaultValue ?? minValue.Value;
		}

		if (maxValue.HasValue && timeSpan > maxValue.Value)
		{
			errors.Add(new ConfigurationValidationError(
				$"Configuration value {timeSpan} exceeds the maximum allowed value {maxValue.Value}",
				key,
				timeSpan,
				$"Set the '{key}' configuration value to at most {maxValue.Value}"));
			return defaultValue ?? maxValue.Value;
		}

		return timeSpan;
	}

	/// <summary>
	/// Validates that a configuration value matches one of the allowed values.
	/// </summary>
	/// <param name="configuration"> The configuration to validate. </param>
	/// <param name="key"> The configuration key. </param>
	/// <param name="allowedValues"> The set of allowed values. </param>
	/// <param name="errors"> The list to add errors Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="comparison"> The string comparison type. </param>
	/// <param name="defaultValue"> The default value if not specified. </param>
	/// <returns> The value if valid, defaultValue otherwise. </returns>
	protected static string? ValidateEnum(
		IConfiguration configuration,
		string key,
		IReadOnlySet<string> allowedValues,
		ICollection<ConfigurationValidationError> errors,
		StringComparison comparison = StringComparison.OrdinalIgnoreCase,
		string? defaultValue = null)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(errors);

		var value = configuration[key];

		if (string.IsNullOrWhiteSpace(value))
		{
			if (!string.IsNullOrWhiteSpace(defaultValue))
			{
				return defaultValue;
			}

			errors.Add(new ConfigurationValidationError(
				"Configuration value is missing",
				key,
				value: null,
				$"Set the '{key}' configuration value to one of: {string.Join(", ", allowedValues)}"));
			return null;
		}

		var isValid = allowedValues.Any(allowed => string.Equals(allowed, value, comparison));

		if (!isValid)
		{
			errors.Add(new ConfigurationValidationError(
				$"Configuration value '{value}' is not valid",
				key,
				value,
				$"Set the '{key}' configuration value to one of: {string.Join(", ", allowedValues)}"));
			return defaultValue;
		}

		return value;
	}
}
