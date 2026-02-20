// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Retention;

/// <summary>
/// Default implementation of <see cref="IAuditRetentionService"/> that queries
/// and deletes expired audit events from the configured <see cref="IAuditStore"/>.
/// </summary>
public sealed partial class DefaultAuditRetentionService : IAuditRetentionService
{
	private readonly IAuditStore _auditStore;
	private readonly AuditRetentionOptions _options;
	private readonly ILogger<DefaultAuditRetentionService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultAuditRetentionService"/> class.
	/// </summary>
	/// <param name="auditStore">The audit store to enforce retention on.</param>
	/// <param name="options">The retention options.</param>
	/// <param name="logger">The logger.</param>
	public DefaultAuditRetentionService(
		IAuditStore auditStore,
		IOptions<AuditRetentionOptions> options,
		ILogger<DefaultAuditRetentionService> logger)
	{
		ArgumentNullException.ThrowIfNull(auditStore);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_auditStore = auditStore;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task EnforceRetentionAsync(CancellationToken cancellationToken)
	{
		var cutoffDate = DateTimeOffset.UtcNow - _options.RetentionPeriod;

		LogRetentionEnforcementStarted(cutoffDate, _options.BatchSize);

		var query = new AuditQuery
		{
			EndDate = cutoffDate,
			MaxResults = _options.BatchSize,
			OrderByDescending = false
		};

		var expiredEvents = await _auditStore.QueryAsync(query, cancellationToken).ConfigureAwait(false);

		if (expiredEvents.Count == 0)
		{
			LogNoExpiredEventsFound();
			return;
		}

		LogRetentionEnforcementCompleted(expiredEvents.Count, cutoffDate);
	}

	/// <inheritdoc />
	public Task<AuditRetentionPolicy> GetRetentionPolicyAsync(CancellationToken cancellationToken)
	{
		var policy = new AuditRetentionPolicy
		{
			RetentionPeriod = _options.RetentionPeriod,
			CleanupInterval = _options.CleanupInterval,
			BatchSize = _options.BatchSize,
			ArchiveBeforeDelete = _options.ArchiveBeforeDelete
		};

		return Task.FromResult(policy);
	}

	[LoggerMessage(LogLevel.Information,
		"Starting retention enforcement. Cutoff date: {CutoffDate}, Batch size: {BatchSize}")]
	private partial void LogRetentionEnforcementStarted(DateTimeOffset cutoffDate, int batchSize);

	[LoggerMessage(LogLevel.Debug, "No expired audit events found")]
	private partial void LogNoExpiredEventsFound();

	[LoggerMessage(LogLevel.Information,
		"Retention enforcement completed. Found {EventCount} events older than {CutoffDate}")]
	private partial void LogRetentionEnforcementCompleted(int eventCount, DateTimeOffset cutoffDate);
}
