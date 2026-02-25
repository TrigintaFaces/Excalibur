// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Text;

using Excalibur.Hosting.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Hosting.Configuration;

/// <summary>
/// Service that validates all registered configuration validators at startup.
/// </summary>
public sealed partial class ConfigurationValidationService : IHostedService
{
	private readonly IConfiguration _configuration;
	private readonly IEnumerable<IConfigurationValidator> _validators;
	private readonly IHostApplicationLifetime _applicationLifetime;
	private readonly ILogger<ConfigurationValidationService> _logger;
	private readonly ConfigurationValidationOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfigurationValidationService" /> class.
	/// </summary>
	/// <param name="configuration"> The application configuration. </param>
	/// <param name="validators"> The collection of configuration validators. </param>
	/// <param name="applicationLifetime"> The application lifetime service. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="options"> The validation options. </param>
	public ConfigurationValidationService(
		IConfiguration configuration,
		IEnumerable<IConfigurationValidator> validators,
		IHostApplicationLifetime applicationLifetime,
		ILogger<ConfigurationValidationService> logger,
		ConfigurationValidationOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(validators);
		ArgumentNullException.ThrowIfNull(applicationLifetime);
		ArgumentNullException.ThrowIfNull(logger);

		_configuration = configuration;
		_validators = validators;
		_applicationLifetime = applicationLifetime;
		_logger = logger;
		_options = options ?? new ConfigurationValidationOptions();
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (!_options.Enabled)
		{
			LogValidationDisabled();
			return;
		}

		LogValidationStarting();

		var allErrors = new List<ConfigurationValidationError>();
		var validatorCount = 0;
		var failedValidators = new List<string>();

		// Sort validators by priority and execute them
		foreach (var validator in _validators.OrderBy(static v => v.Priority).ToList())
		{
			validatorCount++;

			try
			{
				LogRunningValidator(validator.ConfigurationName);

				var result = await validator.ValidateAsync(_configuration, cancellationToken).ConfigureAwait(false);

				if (!result.IsValid)
				{
					failedValidators.Add(validator.ConfigurationName);
					allErrors.AddRange(result.Errors);

					LogValidatorErrors(validator.ConfigurationName, result.Errors.Count);

					// Log individual errors at debug level
					foreach (var error in result.Errors)
					{
						LogValidationError(error.ToString());
					}
				}
				else
				{
					LogValidatorPassed(validator.ConfigurationName);
				}
			}
			catch (Exception ex)
			{
				var errorMessage = $"Validator '{validator.ConfigurationName}' threw an exception: {ex.Message}";

				if (_options.TreatValidatorExceptionsAsErrors)
				{
					allErrors.Add(new ConfigurationValidationError(
						errorMessage,
						validator.ConfigurationName,
						value: null,
						"Check the validator implementation or configuration"));

					failedValidators.Add(validator.ConfigurationName);
					LogValidatorException(ex, validator.ConfigurationName);
				}
				else
				{
					LogValidatorExceptionIgnored(ex, validator.ConfigurationName);
				}
			}
		}

		LogValidationComplete(validatorCount, failedValidators.Count, allErrors.Count);

		// Handle validation results
		if (allErrors.Count > 0)
		{
			var errorReport = BuildErrorReport(allErrors, failedValidators);

			if (_options.FailFast)
			{
				LogValidationFailedTerminating(errorReport);

				// Write to console for visibility
				await Console.Error.WriteLineAsync("CONFIGURATION VALIDATION FAILED").ConfigureAwait(false);
				await Console.Error.WriteLineAsync("==============================").ConfigureAwait(false);
				await Console.Error.WriteLineAsync(errorReport).ConfigureAwait(false);

				// Stop the application
				_applicationLifetime.StopApplication();

				// Throw exception to ensure the application stops
				throw new ConfigurationValidationException(
					"Configuration validation failed with " + allErrors.Count + " error(s). See logs for details.",
					allErrors);
			}

			LogValidationErrorsDetected(errorReport);
		}
		else
		{
			LogAllValidatorsPassed();
		}
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) =>

		// Nothing to clean up
		Task.CompletedTask;

	private static string BuildErrorReport(
		IReadOnlyList<ConfigurationValidationError> errors,
		IReadOnlyList<string> failedValidators)
	{
		var report = new StringBuilder();

		_ = report.AppendLine();
		_ = report.AppendLine("Configuration Validation Report");
		_ = report.AppendLine("================================");
		_ = report.AppendLine();
		_ = report.AppendLine(CultureInfo.InvariantCulture, $"Failed Validators: {string.Join(", ", failedValidators)}");
		_ = report.AppendLine(CultureInfo.InvariantCulture, $"Total Errors: {errors.Count}");
		_ = report.AppendLine();
		_ = report.AppendLine("Errors:");
		_ = report.AppendLine("-------");

		// Group errors by configuration path for better readability
		var groupedErrors = errors
			.GroupBy(static e => e.ConfigurationPath ?? "General", StringComparer.Ordinal)
			.OrderBy(static g => g.Key, StringComparer.Ordinal);

		foreach (var group in groupedErrors)
		{
			_ = report.AppendLine();
			_ = report.AppendLine(CultureInfo.InvariantCulture, $"[{group.Key}]");

			foreach (var error in group)
			{
				_ = report.AppendLine(CultureInfo.InvariantCulture, $" â€¢ {error.Message}");

				if (error.Value != null)
				{
					_ = report.AppendLine(CultureInfo.InvariantCulture, $" Current Value: {error.Value}");
				}

				if (!string.IsNullOrWhiteSpace(error.Recommendation))
				{
					_ = report.AppendLine(CultureInfo.InvariantCulture, $" Recommendation: {error.Recommendation}");
				}
			}
		}

		_ = report.AppendLine();
		_ = report.AppendLine("Action Required:");
		_ = report.AppendLine("----------------");
		_ = report.AppendLine("1. Review the errors above and update your configuration");
		_ = report.AppendLine("2. Check appsettings.json, environment variables, or other configuration sources");
		_ = report.AppendLine("3. Ensure all required configuration values are present and valid");
		_ = report.AppendLine("4. Restart the application after fixing the configuration");

		return report.ToString();
	}

	[LoggerMessage(ExcaliburHostingEventId.ConfigValidationDisabled, LogLevel.Debug, "Configuration validation is disabled")]
	private partial void LogValidationDisabled();

	[LoggerMessage(ExcaliburHostingEventId.ConfigValidationStarting, LogLevel.Information, "Starting configuration validation...")]
	private partial void LogValidationStarting();

	[LoggerMessage(ExcaliburHostingEventId.ConfigValidatorRunning, LogLevel.Debug, "Running validator: {ValidatorName}")]
	private partial void LogRunningValidator(string validatorName);

	[LoggerMessage(ExcaliburHostingEventId.ConfigValidatorErrors, LogLevel.Warning, "Validator {ValidatorName} reported {ErrorCount} error(s)")]
	private partial void LogValidatorErrors(string validatorName, int errorCount);

	[LoggerMessage(ExcaliburHostingEventId.ConfigValidationErrorDetail, LogLevel.Debug, "Validation error: {Error}")]
	private partial void LogValidationError(string error);

	[LoggerMessage(ExcaliburHostingEventId.ConfigValidatorPassed, LogLevel.Debug, "Validator {ValidatorName} passed")]
	private partial void LogValidatorPassed(string validatorName);

	[LoggerMessage(ExcaliburHostingEventId.ConfigValidatorException, LogLevel.Error, "Validator {ValidatorName} threw an exception")]
	private partial void LogValidatorException(Exception ex, string validatorName);

	[LoggerMessage(ExcaliburHostingEventId.ConfigValidatorExceptionIgnored, LogLevel.Warning, "Validator {ValidatorName} threw an exception (ignored)")]
	private partial void LogValidatorExceptionIgnored(Exception ex, string validatorName);

	[LoggerMessage(ExcaliburHostingEventId.ConfigValidationComplete, LogLevel.Information,
		"Configuration validation complete: {TotalValidators} validators, {FailedValidators} failed, {TotalErrors} total error(s)")]
	private partial void LogValidationComplete(int totalValidators, int failedValidators, int totalErrors);

	[LoggerMessage(ExcaliburHostingEventId.ConfigValidationFailedTerminating, LogLevel.Critical, "Configuration validation failed. Application will terminate.\n{ErrorReport}")]
	private partial void LogValidationFailedTerminating(string errorReport);

	[LoggerMessage(ExcaliburHostingEventId.ConfigValidationErrorsDetected, LogLevel.Error, "Configuration validation errors detected:\n{ErrorReport}")]
	private partial void LogValidationErrorsDetected(string errorReport);

	[LoggerMessage(ExcaliburHostingEventId.ConfigAllValidatorsPassed, LogLevel.Information, "All configuration validators passed successfully")]
	private partial void LogAllValidatorsPassed();
}
