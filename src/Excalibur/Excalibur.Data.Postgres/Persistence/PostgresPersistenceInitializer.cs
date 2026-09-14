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
#pragma warning disable IL2026, IL3050 // Serialization/reflection inherently not AOT-safe
			await _provider.InitializeAsync(_options.Value, cancellationToken).ConfigureAwait(false);
#pragma warning restore IL2026, IL3050
			_logger.LogInformation("Postgres persistence provider initialized successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to initialize Postgres persistence provider");

			// Deliberately not rethrown: a database that is briefly unreachable at startup must not stop the
			// application from starting. This warm-up is the only thing that was skipped -- the provider is
			// usable as constructed, so operations attempted later open their own connections and succeed once
			// the database is reachable again.
		}
	}

	/// <inheritdoc/>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
