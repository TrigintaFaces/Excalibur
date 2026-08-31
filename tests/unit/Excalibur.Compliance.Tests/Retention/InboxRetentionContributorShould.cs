// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace Excalibur.Compliance.Tests.Retention;

/// <summary>
/// Safety+liveness lock for <c>InboxRetentionContributor</c>, run against the real (non-mocked)
/// in-memory inbox store through the documented public entry point -- <c>AddExcaliburInbox(i =&gt;
/// i.UseInMemory())</c> plus <c>AddRetentionEnforcement()</c> / <c>AddInboxRetention()</c> -- with zero
/// manual wiring.
/// </summary>
public sealed class InboxRetentionContributorShould
{
	private static readonly IDictionary<string, object> EmptyMetadata = new Dictionary<string, object>(StringComparer.Ordinal);

	[Fact]
	public async Task DeleteAnEntryPastItsRetentionBound_WhileAnEntryStillWithinBoundSurvives()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburInbox(inbox => inbox.UseInMemory());
		_ = services.AddRetentionEnforcement();
		_ = services.AddInboxRetention(o => o.RetentionDays = 90);

		await using var provider = services.BuildServiceProvider();

		var store = provider.GetRequiredService<IInboxStore>();
		var enforcement = provider.GetRequiredService<IRetentionEnforcementService>();

		var now = DateTimeOffset.UtcNow;
		const string ExpiredId = "inbox-retention-expired";
		const string FreshId = "inbox-retention-fresh";
		const string Handler = "handler";

		// CreateEntryAsync returns the exact live entry the store holds, so mutating its Status/
		// ProcessedAt here is a legitimate way to seed a "processed N days ago" fixture through the
		// public surface -- no reflection, no internal reach-around.
		var expired = await store.CreateEntryAsync(ExpiredId, Handler, "t", [], EmptyMetadata, CancellationToken.None);
		expired.Status = InboxStatus.Processed;
		expired.ProcessedAt = now.AddDays(-91);

		var fresh = await store.CreateEntryAsync(FreshId, Handler, "t", [], EmptyMetadata, CancellationToken.None);
		fresh.Status = InboxStatus.Processed;
		fresh.ProcessedAt = now.AddDays(-1);

		var result = await enforcement.EnforceRetentionAsync(CancellationToken.None);

		// SAFETY: the entry past its retention bound was deleted -- proven by the store's own documented
		// duplicate-key contract (CreateEntryAsync throws when the (messageId, handlerType) key already
		// exists), which only holds if the prior "expired" entry is actually gone.
		await Should.NotThrowAsync(() => store.CreateEntryAsync(ExpiredId, Handler, "t", [], EmptyMetadata, CancellationToken.None).AsTask());

		// LIVENESS: the entry still within the retention bound was left untouched.
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => store.CreateEntryAsync(FreshId, Handler, "t", [], EmptyMetadata, CancellationToken.None).AsTask());

		result.RecordsCleaned.ShouldBe(1);
	}

	[Fact]
	public async Task NeverDeleteAnything_WhenRetentionDaysIsZero()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburInbox(inbox => inbox.UseInMemory());
		_ = services.AddRetentionEnforcement();
		_ = services.AddInboxRetention(o => o.RetentionDays = 0);

		await using var provider = services.BuildServiceProvider();

		var store = provider.GetRequiredService<IInboxStore>();
		var enforcement = provider.GetRequiredService<IRetentionEnforcementService>();

		const string Id = "inbox-retention-disabled";
		const string Handler = "handler";
		var entry = await store.CreateEntryAsync(Id, Handler, "t", [], EmptyMetadata, CancellationToken.None);
		entry.Status = InboxStatus.Processed;
		entry.ProcessedAt = DateTimeOffset.UtcNow.AddYears(-5);

		var result = await enforcement.EnforceRetentionAsync(CancellationToken.None);

		result.RecordsCleaned.ShouldBe(0);
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => store.CreateEntryAsync(Id, Handler, "t", [], EmptyMetadata, CancellationToken.None).AsTask());
	}
}
