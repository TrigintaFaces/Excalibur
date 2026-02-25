// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Hosted service that validates transport configuration at application startup.
/// </summary>
/// <remarks>
/// <para>
/// This validator runs during application startup and ensures that:
/// </para>
/// <list type="bullet">
/// <item><description>At least one transport is registered (if validation is enabled)</description></item>
/// <item><description>A default transport is configured when multiple transports are registered</description></item>
/// <item><description>Custom validators pass for each transport (if registered)</description></item>
/// </list>
/// <para>
/// Validation can be configured via <see cref="TransportValidationOptions"/>.
/// Custom validators implementing <see cref="ITransportOptionsValidator"/> can be registered
/// to provide transport-specific validation logic.
/// </para>
/// </remarks>
public sealed partial class TransportStartupValidator : IHostedService
{
	private readonly TransportRegistry _transportRegistry;
	private readonly TransportValidationOptions _options;
	private readonly IEnumerable<ITransportOptionsValidator> _validators;
	private readonly ILogger<TransportStartupValidator> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportStartupValidator"/> class.
	/// </summary>
	/// <param name="transportRegistry">The transport registry to validate.</param>
	/// <param name="options">The validation options.</param>
	/// <param name="validators">Optional custom validators for transport options.</param>
	/// <param name="logger">The logger.</param>
	public TransportStartupValidator(
		TransportRegistry transportRegistry,
		TransportValidationOptions options,
		IEnumerable<ITransportOptionsValidator> validators,
		ILogger<TransportStartupValidator> logger)
	{
		_transportRegistry = transportRegistry ?? throw new ArgumentNullException(nameof(transportRegistry));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_validators = validators ?? [];
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (!_options.ValidateOnStartup)
		{
			LogStartupValidationDisabled();
			return;
		}

		var transportNames = _transportRegistry.GetTransportNames().ToList();
		var transportCount = transportNames.Count;

		LogValidatingConfiguration(transportCount);

		// Validate at least one transport is registered.
		if (_options.RequireAtLeastOneTransport && transportCount == 0)
		{
			throw new InvalidOperationException(Resources.TransportStartupValidator_NoTransportsRegistered);
		}

		// Validate default transport when multiple transports are registered.
		if (_options.RequireDefaultTransportWhenMultiple &&
			transportCount > 1 &&
			!_transportRegistry.HasDefaultTransport)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.TransportStartupValidator_DefaultTransportMissingFormat,
					transportCount,
					string.Join(", ", transportNames)));
		}

		if (_transportRegistry.HasDefaultTransport)
		{
			LogDefaultTransportConfigured(_transportRegistry.DefaultTransportName);
		}

		// Run custom validators for each transport.
		await RunCustomValidatorsAsync(cancellationToken).ConfigureAwait(false);

		LogValidationPassed();
	}

	/// <inheritdoc/>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	private async Task RunCustomValidatorsAsync(CancellationToken cancellationToken)
	{
		var transports = _transportRegistry.GetAllTransports();
		var allErrors = new List<string>();

		foreach (var (transportName, registration) in transports)
		{
			// Find validators that apply to this transport.
			var applicableValidators = _validators.Where(v =>
				string.Equals(v.TransportName, transportName, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(v.TransportName, registration.TransportType, StringComparison.OrdinalIgnoreCase));

			foreach (var validator in applicableValidators)
			{
				LogRunningValidator(transportName);

				var result = await validator.ValidateAsync(registration.Options, cancellationToken)
					.ConfigureAwait(false);

				if (!result.IsValid)
				{
					LogValidatorFailed(transportName, string.Join("; ", result.Errors));
					allErrors.AddRange(result.Errors.Select(e => $"[{transportName}] {e}"));
				}
			}
		}

		if (allErrors.Count > 0)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.TransportStartupValidator_ValidationErrorsFormat,
					allErrors.Count,
					$"{Environment.NewLine}{string.Join(Environment.NewLine, allErrors)}"));
		}
	}

	#region LoggerMessage Definitions

	[LoggerMessage(LogLevel.Debug, "Transport startup validation is disabled")]
	private partial void LogStartupValidationDisabled();

	[LoggerMessage(
		LogLevel.Information,
		"Validating transport configuration: {TransportCount} transport(s) registered")]
	private partial void LogValidatingConfiguration(int transportCount);

	[LoggerMessage(LogLevel.Information, "Default transport configured: {DefaultTransport}")]
	private partial void LogDefaultTransportConfigured(string? defaultTransport);

	[LoggerMessage(LogLevel.Information, "Transport configuration validation passed")]
	private partial void LogValidationPassed();

	[LoggerMessage(LogLevel.Debug, "Running validator for transport '{TransportName}'")]
	private partial void LogRunningValidator(string transportName);

	[LoggerMessage(LogLevel.Warning, "Validation failed for transport '{TransportName}': {Errors}")]
	private partial void LogValidatorFailed(string transportName, string errors);

	#endregion LoggerMessage Definitions
}

/// <summary>
/// Configuration options for transport startup validation.
/// </summary>
public sealed class TransportValidationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to validate transport configuration at startup.
	/// </summary>
	/// <value>True to enable validation; false to skip. Default is true.</value>
	public bool ValidateOnStartup { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether at least one transport must be registered.
	/// </summary>
	/// <value>True to require at least one transport; false to allow zero. Default is false.</value>
	/// <remarks>
	/// <para>
	/// Set this to true in production environments where transport-based messaging is required.
	/// Set to false for testing scenarios or applications that may not use transports.
	/// </para>
	/// </remarks>
	public bool RequireAtLeastOneTransport { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether a default transport must be configured when
	/// multiple transports are registered.
	/// </summary>
	/// <value>True to require a default when multiple transports exist; false to allow ambiguity. Default is true.</value>
	/// <remarks>
	/// <para>
	/// When multiple transports are registered, this option ensures one is designated as the default
	/// to avoid ambiguity when publishing messages without an explicit transport specification.
	/// </para>
	/// </remarks>
	public bool RequireDefaultTransportWhenMultiple { get; set; } = true;
}
