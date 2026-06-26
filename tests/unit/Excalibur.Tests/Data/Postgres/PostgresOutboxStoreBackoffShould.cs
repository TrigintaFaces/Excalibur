// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Data.Postgres;

/// <summary>
/// Author≠impl regression lock for S850 Lane B · <c>q29qfg</c> (Postgres outbox honors a per-message backoff
/// schedule).
/// </summary>
/// <remarks>
/// <para>
/// Authored by FrontendDeveloper (did NOT implement the fix — independence per
/// <c>issue-remediation-protocol</c>) against the frozen GUIDE seam (msg 15508) + the impl pin (15586).
/// <see cref="PostgresOutboxStore"/> now composes the segregated
/// <see cref="IBackoffSchedulableOutboxStore"/> capability: its <c>MarkFailedWithBackoffAsync</c> persists
/// <c>next_attempt_at</c> (via the <c>SetOutboxMessageBackoff</c> request) and the reservation claim gates on
/// <c>next_attempt_at IS NULL OR next_attempt_at &lt;= NOW()</c>, so the computed exponential backoff genuinely
/// throttles re-delivery.
/// </para>
/// <para>
/// <b>RED on the pre-fix surface:</b> pre-fix, <see cref="PostgresOutboxStore"/> did not implement
/// <see cref="IBackoffSchedulableOutboxStore"/> at all — both the capability assertion and the
/// argument-guard contract (reached only through that capability) fail. GREEN once the store composes the
/// capability and validates its inputs. Deterministic + DB-free: the argument guards run before any
/// connection access, so no live Postgres / container is needed. (The SQL predicate + round-trip are covered
/// by Platform's container integration suite, 13/13; this unit lock pins the capability + input contract
/// independently.)
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxStoreBackoffShould
{
	[Fact]
	public void ComposeTheBackoffSchedulableOutboxCapability()
	{
		using var store = CreateStore();

		// object receiver so the assertion compiles even on the pre-fix (sealed, non-implementing) surface,
		// then fails at runtime there (clean RED) rather than as a build break.
		_ = ((object)store).ShouldBeAssignableTo<IBackoffSchedulableOutboxStore>(
			"q29qfg: PostgresOutboxStore must implement IBackoffSchedulableOutboxStore so the computed backoff " +
			"next-attempt time is persisted and the reservation claim gates on it.");
	}

	[Fact]
	public async Task ValidateInputsOnMarkFailedWithBackoff()
	{
		using var store = CreateStore();
		var backoffStore = ((object)store).ShouldBeAssignableTo<IBackoffSchedulableOutboxStore>();

		// The argument guards execute before any IDb access — deterministic and DB-free.
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await backoffStore.MarkFailedWithBackoffAsync(
				string.Empty, "boom", 0, DateTimeOffset.UtcNow, CancellationToken.None));

		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await backoffStore.MarkFailedWithBackoffAsync(
				"msg-1", "boom", -1, DateTimeOffset.UtcNow, CancellationToken.None));
	}

	private static PostgresOutboxStore CreateStore()
	{
		var db = A.Fake<IDb>();
		var options = Options.Create(new PostgresOutboxStoreOptions
		{
			OutboxTableName = "test_outbox",
			DeadLetterTableName = "test_dead_letters",
			ReservationTimeout = 300,
		});

		return new PostgresOutboxStore(db, options, A.Fake<ILogger<PostgresOutboxStore>>(), new PostgresOutboxStoreMetrics());
	}
}
