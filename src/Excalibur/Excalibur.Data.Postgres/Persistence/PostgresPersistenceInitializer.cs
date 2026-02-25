// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Hosted service to initialize the Postgres persistence provider on startup.
/// </summary>
internal sealed class PostgresPersistenceInitializer(
	PostgresPersistenceProvider provider,
	IOptions<PostgresPersistenceOptions> options,
	ILogger<PostgresPersistenceInitializer> logger)
	: IHostedService
{
	private readonly PostgresPersistenceProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));
	private readonly IOptions<PostgresPersistenceOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<PostgresPersistenceInitializer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc/>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			_logger.LogInformation("Initializing Postgres persistence provider");
			await _provider.InitializeAsync(_options.Value, cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("Postgres persistence provider initialized successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to initialize Postgres persistence provider");

			// Don't throw - let the application start even if the database is temporarily unavailable The provider will retry when
			// operations are attempted
		}
	}

	/// <inheritdoc/>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
