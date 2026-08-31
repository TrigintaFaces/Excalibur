// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace Excalibur.Compliance.Tests.Retention;

/// <summary>
/// Safety+liveness lock for <c>OutboxRetentionContributor</c>, run against the real (non-mocked)
/// in-memory outbox store through the documented public entry point -- <c>AddExcalibur(...).AddOutbox(o
/// =&gt; o.UseInMemory())</c> plus <c>AddRetentionEnforcement()</c> / <c>AddOutboxRetention()</c> -- with
/// zero manual wiring.
/// </summary>
public sealed class OutboxRetentionContributorShould
{
	[Fact]
	public async Task DeleteAMessagePastItsRetentionBound_WhileAMessageStillWithinBoundSurvives()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseInMemory()));
		_ = services.AddRetentionEnforcement();
		_ = services.AddOutboxRetention(o => o.RetentionDays = 90);

		await using var provider = services.BuildServiceProvider();

		var store = provider.GetRequiredService<IOutboxStore>();
		var enforcement = provider.GetRequiredService<IRetentionEnforcementService>();

		var now = DateTimeOffset.UtcNow;
		const string ExpiredId = "outbox-retention-expired";
		const string FreshId = "outbox-retention-fresh";

		await store.StageMessageAsync(
			new OutboundMessage { Id = ExpiredId, MessageType = "t", Destination = "d", Status = OutboxStatus.Sent, SentAt = now.AddDays(-91) },
			CancellationToken.None);
		await store.StageMessageAsync(
			new OutboundMessage { Id = FreshId, MessageType = "t", Destination = "d", Status = OutboxStatus.Sent, SentAt = now.AddDays(-1) },
			CancellationToken.None);

		var result = await enforcement.EnforceRetentionAsync(CancellationToken.None);

		// SAFETY: the message past its retention bound was deleted -- proven by the store's own
		// documented duplicate-id contract (StageMessageAsync throws when the id already exists), which
		// only holds if the prior "expired" row is actually gone.
		await Should.NotThrowAsync(() => store.StageMessageAsync(
			new OutboundMessage { Id = ExpiredId, MessageType = "t", Destination = "d" },
			CancellationToken.None).AsTask());

		// LIVENESS: the message still within the retention bound was left untouched -- re-staging under
		// the same id throws, because the row is still present.
		_ = await Should.ThrowAsync<InvalidOperationException>(() => store.StageMessageAsync(
			new OutboundMessage { Id = FreshId, MessageType = "t", Destination = "d" },
			CancellationToken.None).AsTask());

		result.RecordsCleaned.ShouldBe(1);
	}

	[Fact]
	public async Task NeverDeleteAnything_WhenRetentionDaysIsZero()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseInMemory()));
		_ = services.AddRetentionEnforcement();
		_ = services.AddOutboxRetention(o => o.RetentionDays = 0);

		await using var provider = services.BuildServiceProvider();

		var store = provider.GetRequiredService<IOutboxStore>();
		var enforcement = provider.GetRequiredService<IRetentionEnforcementService>();

		const string Id = "outbox-retention-disabled";
		await store.StageMessageAsync(
			new OutboundMessage { Id = Id, MessageType = "t", Destination = "d", Status = OutboxStatus.Sent, SentAt = DateTimeOffset.UtcNow.AddYears(-5) },
			CancellationToken.None);

		var result = await enforcement.EnforceRetentionAsync(CancellationToken.None);

		result.RecordsCleaned.ShouldBe(0);
		_ = await Should.ThrowAsync<InvalidOperationException>(() => store.StageMessageAsync(
			new OutboundMessage { Id = Id, MessageType = "t", Destination = "d" },
			CancellationToken.None).AsTask());
	}
}
