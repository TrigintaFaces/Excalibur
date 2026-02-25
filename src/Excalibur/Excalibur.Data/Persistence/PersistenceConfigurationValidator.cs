// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Hosted service that validates persistence configuration on startup.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PersistenceConfigurationValidator" /> class. </remarks>
internal sealed partial class PersistenceConfigurationValidator(
	IPersistenceConfiguration configuration,
	ILogger<PersistenceConfigurationValidator> logger) : IHostedService
{
	private readonly PersistenceConfiguration _configuration = (configuration as PersistenceConfiguration) ??
															   throw new ArgumentException(
																   "Configuration must be of type PersistenceConfiguration",
																   nameof(configuration));

	private readonly ILogger<PersistenceConfigurationValidator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			LogValidatingConfiguration();

			// Validate the configuration
			_ = _configuration.Validate();

			LogConfigurationValidated(
				_configuration.Providers.Count,
				_configuration.DefaultProvider);

			// Log provider details
			foreach (var (name, config) in _configuration.Providers)
			{
				LogProviderDetails(
					name,
					config.Type.ToString(),
					config.IsReadOnly,
					config.EnableConnectionPooling,
					config.MaxPoolSize);
			}

			return Task.CompletedTask;
		}
		catch (Exception ex)
		{
			LogValidationFailed(ex);
			throw;
		}
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(DataEventId.ConfigurationValidated, LogLevel.Information, "Validating persistence configuration")]
	private partial void LogValidatingConfiguration();

	[LoggerMessage(DataEventId.ProviderTypeConfigured, LogLevel.Information, "Persistence configuration validated successfully. Found {ProviderCount} provider(s), Default: '{DefaultProvider}'")]
	private partial void LogConfigurationValidated(int providerCount, string defaultProvider);

	[LoggerMessage(DataEventId.ConfigurationValidationWarning, LogLevel.Debug, "Provider '{Name}': Type={Type}, ReadOnly={ReadOnly}, Pooling={Pooling}, MaxPool={MaxPool}")]
	private partial void LogProviderDetails(string name, string type, bool readOnly, bool pooling, int maxPool);

	[LoggerMessage(DataEventId.ConfigurationValidationError, LogLevel.Error, "Persistence configuration validation failed")]
	private partial void LogValidationFailed(Exception ex);
}
