// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Retention;

/// <summary>
/// Retention contributor for the inbox: deletes processed entries older than
/// <see cref="InboxRetentionOptions.RetentionDays"/> via the store-agnostic
/// <see cref="IInboxStoreAdmin"/> admin surface every registered inbox provider already implements.
/// </summary>
/// <remarks>
/// Registered by <c>AddInboxRetention</c>. Mirrors <see cref="OutboxRetentionContributor"/>.
/// </remarks>
internal sealed class InboxRetentionContributor(
	IInboxStoreAdmin admin,
	IOptions<InboxRetentionOptions> options) : IRetentionContributor
{
	/// <inheritdoc/>
	public string Name => "Inbox";

	/// <inheritdoc/>
	public async Task<RetentionContributorResult> EnforceAsync(
		RetentionContributorContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		var retentionDays = options.Value.RetentionDays;
		if (retentionDays <= 0 || context.DryRun)
		{
			return RetentionContributorResult.Succeeded(0);
		}

		var cutoff = context.AsOf.AddDays(-retentionDays);
		var removed = await admin.CleanupAllTenantsProcessedEntriesAsync(cutoff, cancellationToken)
			.ConfigureAwait(false);
		return RetentionContributorResult.Succeeded(removed);
	}
}
