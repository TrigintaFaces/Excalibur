// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Retention;

/// <summary>
/// Implementation of <see cref="IRetentionEnforcementService"/> that enforces the retention policies the
/// host has declared and hands them to the registered <see cref="IRetentionContributor"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// The policy population is exactly the union of the host's declarations
/// (<c>AddRetentionPolicies&lt;T&gt;()</c> / <c>AddRetentionPoliciesFromAssembly(Assembly)</c>). It is never
/// discovered from the assemblies the process happens to have loaded.
/// </para>
/// </remarks>
internal sealed partial class RetentionEnforcementService : IRetentionEnforcementService
{
	private readonly IOptions<RetentionEnforcementOptions> _options;
	private readonly IReadOnlyList<RetentionPolicy> _policies;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<RetentionEnforcementService> _logger;
	private readonly IReadOnlyList<IRetentionContributor> _contributors;

	/// <summary>
	/// Initializes a new instance of the <see cref="RetentionEnforcementService"/> class.
	/// </summary>
	/// <param name="options">The retention enforcement options.</param>
	/// <param name="declarations">The host-declared retention scope.</param>
	/// <param name="timeProvider">The clock enforcement passes are evaluated against.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="contributors">
	/// The registered store-specific retention contributors that perform the actual deletion of expired
	/// data. When none are registered, enforcement logs a warning and reports zero records cleaned.
	/// </param>
	public RetentionEnforcementService(
		IOptions<RetentionEnforcementOptions> options,
		IEnumerable<RetentionPolicyDeclaration> declarations,
		TimeProvider timeProvider,
		ILogger<RetentionEnforcementService> logger,
		IEnumerable<IRetentionContributor>? contributors = null)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		ArgumentNullException.ThrowIfNull(declarations);
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_contributors = contributors is null ? [] : [.. contributors];

		// Declaring the same type twice (directly and through its assembly) must not double its policies.
		_policies = [.. declarations.SelectMany(static d => d.Policies).Distinct()];
	}

	/// <inheritdoc />
	public async Task<RetentionEnforcementResult> EnforceRetentionAsync(
		CancellationToken cancellationToken)
	{
		var dryRun = _options.Value.DryRun;
		LogRetentionEnforcementStarted(dryRun);

		try
		{
			var policies = _policies;

			// Enforcement is policy-driven by the framework, but the actual data-store deletion is performed
			// by registered IRetentionContributor implementations (mirrors the IErasureContributor seam).
			// When none are registered we MUST NOT report success while deleting nothing — log a warning and
			// return zero records cleaned (honest contract; MS-bar: build the fix, never a silent no-op).
			if (_contributors.Count == 0)
			{
				LogRetentionNoContributorsRegistered(policies.Count);

				return new RetentionEnforcementResult
				{
					PoliciesEvaluated = policies.Count,
					RecordsCleaned = 0,
					IsDryRun = dryRun,
					CompletedAt = _timeProvider.GetUtcNow(),
				};
			}

			var context = new RetentionContributorContext
			{
				Policies = policies,
				DryRun = dryRun,
				AsOf = _timeProvider.GetUtcNow(),
			};

			var totalRecordsCleaned = 0;
			var succeededCount = 0;
			var failedCount = 0;

			// Fail-open per contributor: a single contributor's failure must not abort the others
			// (mirrors ErasureService). Failures are logged; the overall pass still reports what was cleaned.
			foreach (var contributor in _contributors)
			{
				try
				{
					// A contributor that does not consume declared policies is handed an EMPTY list, so it cannot
					// act on a population it said it does not use.
					var contributorContext = contributor.ConsumesDeclaredPolicies
						? context
						: context with { Policies = [] };

					var result = await contributor.EnforceAsync(contributorContext, cancellationToken).ConfigureAwait(false);

					if (result.Success)
					{
						totalRecordsCleaned += result.RecordsCleaned;
						succeededCount++;
						LogRetentionContributorCompleted(contributor.Name, result.RecordsCleaned, dryRun);
					}
					else
					{
						failedCount++;
						LogRetentionContributorFailed(contributor.Name, result.ErrorMessage ?? "Unknown error", null);
					}
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					failedCount++;
					LogRetentionContributorFailed(contributor.Name, ex.Message, ex);
				}
			}

			// when contributors ran but every one failed (nothing cleaned), a bare Information
			// 'completed' misreports a failed enforcement pass as success. Log at Warning instead so an
			// operator sees the sink failure; otherwise report normal completion.
			if (_contributors.Count > 0 && succeededCount == 0 && failedCount > 0)
			{
				LogRetentionEnforcementAllContributorsFailed(failedCount, policies.Count);
			}
			else
			{
				LogRetentionEnforcementCompleted(policies.Count, dryRun);
			}

			return new RetentionEnforcementResult
			{
				PoliciesEvaluated = policies.Count,
				RecordsCleaned = totalRecordsCleaned,
				IsDryRun = dryRun,
				CompletedAt = _timeProvider.GetUtcNow(),
			};
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogRetentionEnforcementFailed(ex);
			throw;
		}
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<RetentionPolicy>> GetRetentionPoliciesAsync(
		CancellationToken cancellationToken) => Task.FromResult(_policies);

	[LoggerMessage(
		ComplianceEventId.RetentionEnforcementStarted,
		LogLevel.Information,
		"Starting retention enforcement scan, dry run: {DryRun}")]
	private partial void LogRetentionEnforcementStarted(bool dryRun);

	[LoggerMessage(
		ComplianceEventId.RetentionEnforcementCompleted,
		LogLevel.Information,
		"Retention enforcement scan completed. Policies evaluated: {PolicyCount}, dry run: {DryRun}")]
	private partial void LogRetentionEnforcementCompleted(int policyCount, bool dryRun);

	[LoggerMessage(
		ComplianceEventId.RetentionEnforcementAllContributorsFailed,
		LogLevel.Warning,
		"Retention enforcement scan completed but ALL {FailedCount} contributor(s) failed — no data was cleaned. Policies evaluated: {PolicyCount}. Investigate the retention sink(s).")]
	private partial void LogRetentionEnforcementAllContributorsFailed(int failedCount, int policyCount);

	[LoggerMessage(
		ComplianceEventId.RetentionEnforcementFailed,
		LogLevel.Error,
		"Retention enforcement scan failed")]
	private partial void LogRetentionEnforcementFailed(Exception exception);

	[LoggerMessage(
		ComplianceEventId.RetentionNoContributorsRegistered,
		LogLevel.Warning,
		"Retention enforcement is enabled and evaluated {PolicyCount} policies, but no IRetentionContributor is registered — no data was deleted. Register a retention contributor to enforce cleanup.")]
	private partial void LogRetentionNoContributorsRegistered(int policyCount);

	[LoggerMessage(
		ComplianceEventId.RetentionContributorCompleted,
		LogLevel.Information,
		"Retention contributor {ContributorName} cleaned {RecordsCleaned} record(s), dry run: {DryRun}")]
	private partial void LogRetentionContributorCompleted(string contributorName, int recordsCleaned, bool dryRun);

	[LoggerMessage(
		ComplianceEventId.RetentionContributorFailed,
		LogLevel.Error,
		"Retention contributor {ContributorName} failed: {Error}")]
	private partial void LogRetentionContributorFailed(string contributorName, string error, Exception? exception);
}
