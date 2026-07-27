// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Spanner.Data;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Spanner;

/// <summary>
/// Default <see cref="ISpannerConnectionProvider"/>: builds the Spanner connection string from
/// <see cref="SpannerOptions"/> and replays retryable (<c>ABORTED</c>) transactions with exponential backoff.
/// </summary>
internal sealed class SpannerConnectionProvider : ISpannerConnectionProvider
{
	private readonly SpannerOptions _options;
	private readonly string _connectionString;

	public SpannerConnectionProvider(IOptions<SpannerOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;

		// Honor an explicit emulator endpoint. The Spanner SDK auto-detects the emulator from
		// SPANNER_EMULATOR_HOST; setting the option surfaces the same behavior without requiring the
		// environment to be pre-configured (used by local development and the integration-test fixture).
		if (!string.IsNullOrWhiteSpace(_options.EmulatorHost))
		{
			Environment.SetEnvironmentVariable("SPANNER_EMULATOR_HOST", _options.EmulatorHost);
		}

		_connectionString = new SpannerConnectionStringBuilder
		{
			DataSource = _options.DatabasePath,
		}.ConnectionString;
	}

	public SpannerConnection CreateConnection() => new(_connectionString);

	public async Task<T> ExecuteInRetryableTransactionAsync<T>(
		Func<SpannerConnection, CancellationToken, Task<T>> operation,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);

		var attempt = 0;
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			await using var connection = CreateConnection();
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			try
			{
				return await operation(connection, cancellationToken).ConfigureAwait(false);
			}
			catch (SpannerException ex) when (ex.IsRetryable && attempt < _options.MaxAbortRetries)
			{
				attempt++;
				var backoffMilliseconds = _options.AbortRetryBaseDelayMilliseconds * Math.Pow(2, attempt - 1);
				await Task.Delay(TimeSpan.FromMilliseconds(backoffMilliseconds), cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
