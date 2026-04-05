// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

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
/// </list>
/// <para>
/// Validation can be configured via <see cref="TransportValidationOptions"/>.
/// Transport-specific options validation should use <c>IValidateOptions&lt;T&gt;</c>
/// with <c>ValidateOnStart</c> instead of custom validator interfaces.
/// </para>
/// </remarks>
internal sealed partial class TransportStartupValidator : IHostedService
{
	private readonly ITransportRegistry _transportRegistry;
	private readonly TransportValidationOptions _options;
	private readonly ILogger<TransportStartupValidator> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportStartupValidator"/> class.
	/// </summary>
	/// <param name="transportRegistry">The transport registry to validate.</param>
	/// <param name="options">The validation options.</param>
	/// <param name="logger">The logger.</param>
	internal TransportStartupValidator(
		ITransportRegistry transportRegistry,
		TransportValidationOptions options,
		ILogger<TransportStartupValidator> logger)
	{
		_transportRegistry = transportRegistry ?? throw new ArgumentNullException(nameof(transportRegistry));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (!_options.ValidateOnStartup)
		{
			LogStartupValidationDisabled();
			return Task.CompletedTask;
		}

		var transportNames = MaterializeTransportNames(_transportRegistry.GetTransportNames());
		var transportCount = transportNames.Length;

		LogValidatingConfiguration(transportCount);

		// Validate at least one transport is registered.
		if (_options.RequireAtLeastOneTransport && transportCount == 0)
		{
			throw new InvalidOperationException(Resources.TransportStartupValidator_NoTransportsRegistered);
		}

		// Validate default transport when multiple transports are registered.
		var hasDefault = _transportRegistry.HasDefaultTransport;
		var defaultName = _transportRegistry.DefaultTransportName;

		if (_options.RequireDefaultTransportWhenMultiple &&
			transportCount > 1 &&
			!hasDefault)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.TransportStartupValidator_DefaultTransportMissingFormat,
					transportCount,
					string.Join(", ", transportNames)));
		}

		if (hasDefault)
		{
			LogDefaultTransportConfigured(defaultName);
		}

		LogValidationPassed();
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	private static string[] MaterializeTransportNames(IEnumerable<string> transportNames)
	{
		if (transportNames is string[] namesArray)
		{
			return namesArray;
		}

		if (transportNames is ICollection<string> namesCollection)
		{
			if (namesCollection.Count == 0)
			{
				return [];
			}

			var names = new string[namesCollection.Count];
			namesCollection.CopyTo(names, 0);
			return names;
		}

		var bufferedNames = new List<string>();
		foreach (var transportName in transportNames)
		{
			bufferedNames.Add(transportName);
		}

		return bufferedNames.ToArray();
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
