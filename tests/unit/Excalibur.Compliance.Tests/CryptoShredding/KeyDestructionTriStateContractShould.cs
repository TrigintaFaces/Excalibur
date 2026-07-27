// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Encryption;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// Independent (author != implementer) contract lock for the tri-state key-destruction outcome
/// (nu7nrf AC2). Bound to emitted behaviour over the REAL <see cref="InMemoryKeyManagementProvider"/>
/// — no mocks. Proves <see cref="IKeyManagementProvider.DeleteKeyAsync"/> distinguishes
/// "destroyed now (irrecoverable)" from "scheduled (recoverable until T)" from "not found", instead of
/// an undifferentiated <c>bool</c> that let the AWS provider silently clamp <c>retentionDays:0</c> to a
/// 7-day floor while returning <see langword="true"/> — the false-GDPR-attestation root cause.
/// </summary>
/// <remarks>
/// RED against the pre-fix surface: <see cref="IKeyManagementProvider.DeleteKeyAsync"/> previously
/// returned <c>Task&lt;bool&gt;</c>, so a caller could not tell immediate destruction from a scheduled
/// window. These assertions are inexpressible against the old contract.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyDestructionTriStateContractShould
{
	private static InMemoryKeyManagementProvider CreateProviderWithDefaultKey()
	{
		var provider = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
		// Materialize the auto-generated default key so it exists in the store before deletion.
		#pragma warning disable RS0030 // bd-c36hwe: sync-over-async debt (migrate to await/poll)
		_ = provider.GetKeyAsync("default", CancellationToken.None).GetAwaiter().GetResult();
		#pragma warning restore RS0030
		return provider;
	}

	[Fact]
	public async Task DeleteKey_ZeroRetention_OnImmediateProvider_IsCompletedAndIrreversibleNow()
	{
		using var provider = CreateProviderWithDefaultKey();

		var outcome = await provider.DeleteKeyAsync("default", retentionDays: 0, CancellationToken.None);

		outcome.State.ShouldBe(KeyDestructionState.Completed);
		outcome.IsIrreversibleNow.ShouldBeTrue("an immediate provider destroying with retentionDays:0 is irrecoverable on return");
		_ = outcome.IrreversibleAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task DeleteKey_NonZeroRetention_IsScheduledIrreversible_NotIrreversibleNow()
	{
		using var provider = CreateProviderWithDefaultKey();

		var before = DateTimeOffset.UtcNow;
		var outcome = await provider.DeleteKeyAsync("default", retentionDays: 7, CancellationToken.None);

		outcome.State.ShouldBe(KeyDestructionState.ScheduledIrreversible);
		outcome.IsIrreversibleNow.ShouldBeFalse("a scheduled deletion is recoverable until the retention window elapses — it is NOT irrecoverable now");
		_ = outcome.IrreversibleAt.ShouldNotBeNull();
		outcome.IrreversibleAt!.Value.ShouldBeGreaterThan(before.AddDays(7).AddMinutes(-1),
			"the effective-destruction time (retention floor) must be surfaced, not silently clamped away");
	}

	[Fact]
	public async Task DeleteKey_UnknownKey_IsNotFound()
	{
		using var provider = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);

		var outcome = await provider.DeleteKeyAsync("no-such-key", retentionDays: 0, CancellationToken.None);

		outcome.State.ShouldBe(KeyDestructionState.NotFound);
		outcome.IsIrreversibleNow.ShouldBeFalse("a key that does not exist was not destroyed");
	}
}
