// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.CosmosDb.Tests.ChangeFeed;

/// <summary>
/// Regression lock for <c>bd-aa1ufr</c> (S856): <c>AddCosmosDbChangeFeedDurabilityDefaults</c> must be
/// <b>public</b> so that Cosmos entry points (ES, Outbox, data provider) can call it and consumers always
/// resolve a non-null <see cref="IChangeFeedCheckpointStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pre-fix behaviour (RED on HEAD):</b> <c>AddCosmosDbChangeFeedDurabilityDefaults</c> was declared
/// <c>private</c> (scoped inside <c>RegisterCoreServices</c>), making it unreachable from entry-point
/// callers that don't go through the full CosmosDb registration path. An ES-only or Outbox-only consumer
/// therefore had no <see cref="IChangeFeedCheckpointStore"/> in the container → silent null →
/// durable change-feed continuation silently inert (bd-egwtku / bd-ydln24 regression).
/// </para>
/// <para>
/// <b>Post-fix behaviour (GREEN):</b> the method is promoted to <c>public static</c>, callable directly
/// from any entry point. It uses <c>TryAddSingleton</c>, making it idempotent (safe to call from multiple
/// Cosmos entry points without duplicating the registration).
/// </para>
/// <para>
/// These tests are DI-container-only (no real infrastructure). They prove the registration contract, not
/// the runtime durability behaviour (the real-infra lock is <c>jattxa</c>).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class CosmosDbChangeFeedDurabilityDefaultsShould
{
	[Fact]
	public void RegisterIChangeFeedCheckpointStore_WhenCalledDirectly()
	{
		// Arrange — ES-only or Outbox-only consumer scenario: only AddCosmosDbChangeFeedDurabilityDefaults
		// is called (no full AddExcaliburCosmosDb). The checkpoint store must still be resolvable.
		// Non-vacuous: pre-fix → method is private/inaccessible from outside RegisterCoreServices →
		// calling it from test would fail to compile (or not register) → GetRequiredService throws → RED.
		var services = new ServiceCollection();

		// Act
		services.AddCosmosDbChangeFeedDurabilityDefaults();

		using var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetRequiredService<IChangeFeedCheckpointStore>();
		store.ShouldNotBeNull(
			"bd-aa1ufr: AddCosmosDbChangeFeedDurabilityDefaults must register a non-null IChangeFeedCheckpointStore " +
			"so that ES-only/Outbox-only consumers do not receive a null checkpoint store at runtime.");
	}

	[Fact]
	public void RegisterExactlyOneCheckpointStore_WhenCalledMultipleTimes()
	{
		// Arrange — multiple entry points calling AddCosmosDbChangeFeedDurabilityDefaults (e.g. ES + Outbox).
		// The method uses TryAddSingleton, so the second call is a no-op (idempotent).
		// Non-vacuous: if AddSingleton (not TryAdd) were used, two descriptors would be registered,
		// making resolution non-deterministic → count != 1 → assertion fails → RED.
		var services = new ServiceCollection();

		// Act — simulate two entry points each calling the shared durability helper
		services.AddCosmosDbChangeFeedDurabilityDefaults(); // first entry point (e.g. ES)
		services.AddCosmosDbChangeFeedDurabilityDefaults(); // second entry point (e.g. Outbox)

		// Assert — exactly one IChangeFeedCheckpointStore descriptor must remain.
		var count = services.Count(sd => sd.ServiceType == typeof(IChangeFeedCheckpointStore));
		count.ShouldBe(1,
			"bd-aa1ufr: AddCosmosDbChangeFeedDurabilityDefaults must be idempotent (TryAddSingleton). " +
			"Multiple calls from different Cosmos entry points must not register duplicate stores.");
	}

	[Fact]
	public void ResolveTheSameInstance_WhenCalledMultipleTimes()
	{
		// Arrange — verify singleton lifetime: the same instance is returned on each resolve.
		var services = new ServiceCollection();
		services.AddCosmosDbChangeFeedDurabilityDefaults();
		services.AddCosmosDbChangeFeedDurabilityDefaults(); // idempotent second call

		using var provider = services.BuildServiceProvider();

		// Act
		var store1 = provider.GetRequiredService<IChangeFeedCheckpointStore>();
		var store2 = provider.GetRequiredService<IChangeFeedCheckpointStore>();

		// Assert — singleton contract: both resolves return the same instance.
		store1.ShouldBeSameAs(store2,
			"bd-aa1ufr: IChangeFeedCheckpointStore must be registered as a Singleton (TryAddSingleton). " +
			"Multiple resolutions must return the same instance.");
	}
}
