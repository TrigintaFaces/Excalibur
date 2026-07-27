// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Retention;

/// <summary>
/// Default implementation of <see cref="IAuditRetentionService"/> that removes expired audit events from
/// the configured <see cref="IAuditStore"/> through its <see cref="IAuditPurgeCapability"/>.
/// </summary>
/// <remarks>
/// Enforcement is estate-wide: every tenant's expired events are removed by a single pass, because a
/// retention policy is a statement about how long data may be kept rather than about who may read it.
/// A store that does not provide the purge capability causes enforcement to fail loudly — this service
/// never reports a completed pass it did not perform.
/// </remarks>
public sealed partial class DefaultAuditRetentionService : IAuditRetentionService
{
	private readonly IAuditStore _auditStore;
	private readonly AuditRetentionOptions _options;
	private readonly ILogger<DefaultAuditRetentionService> _logger;
	private readonly TimeProvider _timeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultAuditRetentionService"/> class.
	/// </summary>
	/// <param name="auditStore">The audit store to enforce retention on.</param>
	/// <param name="options">The retention options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="timeProvider">
	/// The time source used to compute the retention cutoff. Defaults to <see cref="TimeProvider.System"/>.
	/// The cutoff decides which audit records are deleted, so the clock behind it is a dependency rather than
	/// an ambient detail: supplying a controlled provider is what allows that boundary to be asserted exactly
	/// instead of approximated against a window that races the wall clock.
	/// </param>
	public DefaultAuditRetentionService(
		IAuditStore auditStore,
		IOptions<AuditRetentionOptions> options,
		ILogger<DefaultAuditRetentionService> logger,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(auditStore);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_auditStore = auditStore;
		_options = options.Value;
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;
	}

	/// <inheritdoc />
	/// <exception cref="NotSupportedException">
	/// The configured audit store does not provide <see cref="IAuditPurgeCapability"/>, so retention cannot
	/// be enforced against it.
	/// </exception>
	public async Task EnforceRetentionAsync(CancellationToken cancellationToken)
	{
		// An explicit opt-out is honoured BEFORE anything is resolved or deleted, and it is announced rather
		// than silently obeyed. This check exists because the deletion above is real: while retention was
		// inert, ignoring this flag was harmless — a consumer who disabled enforcement got no deletion by
		// accident. Once the purge works, ignoring it destroys audit data against a written instruction,
		// which is a worse outcome than the inertness that was fixed. The log line is not decoration: a
		// disabled control that says nothing is indistinguishable from a broken one.
		if (!_options.EnableRetentionEnforcement)
		{
			LogRetentionDisabled();
			return;
		}

		var cutoffDate = _timeProvider.GetUtcNow() - _options.RetentionPeriod;

		// Resolved, never cast, so the capability survives decoration (an encrypting or RBAC wrapper
		// forwards GetService to the store beneath it).
		//
		// A store that cannot purge FAILS THE PASS — it does not skip it quietly. This is the whole of
		// this bug: retention previously queried the expired events, logged "enforcement completed", and
		// deleted nothing, so an operator watching the logs had positive evidence of a control that was
		// not running. Over-retained audit data is a regulatory exposure, and the log line was the reason
		// nobody looked. A loud failure is recoverable; a false receipt is not, because nothing prompts
		// anyone to investigate a success.
		if (_auditStore.GetService(typeof(IAuditPurgeCapability)) is not IAuditPurgeCapability purge)
		{
			LogRetentionUnsupported(_auditStore.GetType().Name);

			throw new NotSupportedException(
				$"Audit retention is configured, but the audit store '{_auditStore.GetType().Name}' does not "
				+ "provide a purge capability, so no data can be removed. Register a store that supports "
				+ "purging, or do not register the retention background service — a retention pass that "
				+ "cannot delete must not report success.");
		}

		LogRetentionEnforcementStarted(cutoffDate, _options.BatchSize);

		// Estate-wide by contract: retention governs how long anything may be kept, which is a property of
		// the data and its policy rather than of who may read it. Sweeping a single partition would leave
		// every other tenant's expired rows in place while reporting a completed pass, so this deliberately
		// calls the member whose NAME declares the estate-wide scope — the scoped member exists for targeted
		// erasure and using it here would be the same false-completion defect wearing a tenant argument.
		//
		// The count reported below is the number of events the store ACTUALLY DELETED, not the number found
		// by a query. Those were previously the same log line with different meanings, which is how a pass
		// that deleted nothing reported a number that looked like work.
		var deleted = await purge.PurgeExpiredAsync(cutoffDate, cancellationToken).ConfigureAwait(false);

		if (deleted == 0)
		{
			LogNoExpiredEventsFound();
			return;
		}

		LogRetentionEnforcementCompleted(deleted, cutoffDate);
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

	[LoggerMessage(LogLevel.Warning,
		"Audit retention enforcement is DISABLED by configuration (EnableRetentionEnforcement = false). "
		+ "No expired audit events will be removed. Expired data is being retained deliberately.")]
	private partial void LogRetentionDisabled();

	[LoggerMessage(LogLevel.Critical,
		"Audit retention cannot run: store {AuditStoreType} provides no purge capability. No audit data "
		+ "has been removed and none will be until a purge-capable store is configured.")]
	private partial void LogRetentionUnsupported(string auditStoreType);

	// "Deleted", not "Found". The parameter now carries the number of events the store actually removed,
	// and the old wording described the number a query returned — the two were the same line with different
	// meanings, which is how a pass that deleted nothing reported a number that read like work. Leaving the
	// text as "Found" would preserve the misreading after the behaviour behind it had been corrected.
	[LoggerMessage(LogLevel.Information,
		"Retention enforcement completed. Deleted {EventCount} audit events older than {CutoffDate}")]
	private partial void LogRetentionEnforcementCompleted(int eventCount, DateTimeOffset cutoffDate);
}
