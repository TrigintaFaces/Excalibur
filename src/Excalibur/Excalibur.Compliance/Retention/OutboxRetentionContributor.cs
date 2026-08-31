// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Retention;

/// <summary>
/// Retention contributor for the outbox: deletes sent messages older than
/// <see cref="OutboxRetentionOptions.RetentionDays"/> via the store-agnostic
/// <see cref="IOutboxStoreAdmin"/> admin surface every registered outbox provider already implements.
/// </summary>
/// <remarks>
/// Registered by <c>AddOutboxRetention</c>. Unlike the erasure side (which needs
/// <c>[PersonalData]</c> reflection over a specific aggregate type), outbox/inbox retention deletes by
/// age across every tenant using the same primitive the outbox's own background cleanup would use, so a
/// single contributor covers every outbox provider without per-provider code.
/// </remarks>
internal sealed class OutboxRetentionContributor(
	IOutboxStoreAdmin admin,
	IOptions<OutboxRetentionOptions> options) : IRetentionContributor
{
	/// <inheritdoc/>
	public string Name => "Outbox";

	/// <inheritdoc/>
	public async Task<RetentionContributorResult> EnforceAsync(
		RetentionContributorContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		var retentionDays = options.Value.RetentionDays;
		if (retentionDays <= 0 || context.DryRun)
		{
			// Disabled, or a dry run: never delete. RecordsCleaned=0 is literally true in both cases --
			// the contract this contributor is bound to (IRetentionContributor's remarks) is that it
			// never reports success while deleting nothing.
			return RetentionContributorResult.Succeeded(0);
		}

		var cutoff = context.AsOf.AddDays(-retentionDays);
		var batchSize = options.Value.BatchSize;
		var total = 0;
		int removed;
		do
		{
			removed = await admin.CleanupAllTenantsSentMessagesAsync(cutoff, batchSize, cancellationToken)
				.ConfigureAwait(false);
			total += removed;
		}
		while (removed >= batchSize && !cancellationToken.IsCancellationRequested);

		return RetentionContributorResult.Succeeded(total);
	}
}
