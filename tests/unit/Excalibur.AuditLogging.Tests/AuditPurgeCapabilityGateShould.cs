// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.AuditLogging.Retention;
using Excalibur.Compliance;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// Binds the fail-closed contract for vaaol0: <c>AddAuditRetention</c> over a store that cannot purge
/// must refuse startup, not fail silently on the first <see cref="AuditRetentionOptions.CleanupInterval"/>.
/// </summary>
/// <remarks>
/// Every arm resolves the options through a real <see cref="ServiceProvider"/>, so the assertion binds
/// observable behaviour rather than the validator's own arithmetic. Both fake stores are durable (answer
/// <see cref="IDurableAuditStore"/>) so the pre-existing durability gate never fires here -- these arms
/// isolate the purge-capability gate specifically.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditPurgeCapabilityGateShould
{
	private static AuditRetentionOptions Resolve(IServiceProvider provider) =>
		provider.GetRequiredService<IOptions<AuditRetentionOptions>>().Value;

	// ---------- SAFETY ----------

	[Fact]
	public void Fail_closed_when_the_store_cannot_purge_and_enforcement_is_enabled()
	{
		// The fail-closed property this bead exists to add: a purge-incapable store, with retention
		// enforcement left at its default (true), must refuse startup rather than throwing hours later
		// on the first CleanupInterval. RED before the fix (nothing refused it).
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IAuditStore, FakePurgeIncapableAuditStore>();
		_ = services.AddAuditRetention(o => o.CleanupInterval = TimeSpan.FromHours(1));

		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<OptionsValidationException>(() => Resolve(provider));
		ex.Message.ShouldContain(nameof(FakePurgeIncapableAuditStore));
	}

	// ---------- LIVENESS ----------

	[Fact]
	public void Start_when_the_store_can_purge()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IAuditStore, FakePurgeCapableAuditStore>();
		_ = services.AddAuditRetention(o => o.CleanupInterval = TimeSpan.FromHours(1));

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider),
			"a purge-capable store answers IAuditPurgeCapability, so the requirement is satisfied");
	}

	[Fact]
	public void Start_over_a_purge_incapable_store_when_enforcement_is_disabled()
	{
		// Enforcement off is the existing, documented escape hatch (EnableRetentionEnforcement = false) --
		// the gate must not turn that into a second, redundant refusal.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IAuditStore, FakePurgeIncapableAuditStore>();
		_ = services.AddAuditRetention(o =>
		{
			o.CleanupInterval = TimeSpan.FromHours(1);
			o.EnableRetentionEnforcement = false;
		});

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider),
			"enforcement is explicitly disabled, so DefaultAuditRetentionService never resolves the "
			+ "capability and there is nothing for this gate to protect");
	}

	/// <summary>
	/// A durable store that also answers <see cref="IAuditPurgeCapability"/> directly (no first-party
	/// base supplies it) -- the capable half of the capability query.
	/// </summary>
	private sealed class FakePurgeCapableAuditStore : IAuditStore, IDurableAuditStore, IAuditPurgeCapability
	{
		public Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken) =>
			Task.FromResult(default(AuditEventId));

		public Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken) =>
			Task.FromResult<AuditEvent?>(null);

		public Task<IReadOnlyList<AuditEvent>> QueryAsync(
			AuditQuery query,
			CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<AuditEvent>>([]);

		public Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken) =>
			Task.FromResult(0L);

		public Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
			DateTimeOffset startDate,
			DateTimeOffset endDate,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException("Not exercised by the purge-capability gate.");

		public Task<AuditEvent?> GetLastEventAsync(CancellationToken cancellationToken) =>
			Task.FromResult<AuditEvent?>(null);

		public Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<int> PurgeTenantAsync(
			DateTimeOffset cutoff,
			KeyedTenantPartition tenant,
			CancellationToken cancellationToken) =>
			Task.FromResult(0);
	}

	/// <summary>
	/// A durable store that does NOT answer <see cref="IAuditPurgeCapability"/> -- the store the gate must
	/// refuse when retention enforcement is left enabled.
	/// </summary>
	private sealed class FakePurgeIncapableAuditStore : IAuditStore, IDurableAuditStore
	{
		public Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken) =>
			Task.FromResult(default(AuditEventId));

		public Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken) =>
			Task.FromResult<AuditEvent?>(null);

		public Task<IReadOnlyList<AuditEvent>> QueryAsync(
			AuditQuery query,
			CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<AuditEvent>>([]);

		public Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken) =>
			Task.FromResult(0L);

		public Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
			DateTimeOffset startDate,
			DateTimeOffset endDate,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException("Not exercised by the purge-capability gate.");

		public Task<AuditEvent?> GetLastEventAsync(CancellationToken cancellationToken) =>
			Task.FromResult<AuditEvent?>(null);
	}
}
