// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// Binds the fail-closed contract for audit-store durability against the capability-query seam: a host
/// that never states a durability intention gets a startup refusal, not a silent volatile audit trail.
/// </summary>
/// <remarks>
/// <para>
/// Durability is now proven by asking the registered store — <c>GetService(typeof(IDurableAuditStore))</c>
/// answers non-null on a durable store and null on a volatile one — not by a separately-registered marker.
/// Every arm resolves the options through a <em>real</em> <see cref="ServiceProvider" /> built from the
/// production registration path, so the assertion binds whether the registration wires the validator, not
/// merely the validator's own arithmetic.
/// </para>
/// <para>
/// The refusal arm is the fail-closed safety property and doubles as the design's non-vacuity proof: a
/// volatile store registered with no durability intent MUST refuse startup. The accept arms are the
/// liveness half — a gate that refused every configuration would satisfy safety and make the package
/// unusable, and a safety-only suite would not notice.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditStoreDurabilityGateShould
{
	private static AuditLoggingOptions Resolve(IServiceProvider provider) =>
		provider.GetRequiredService<IOptions<AuditLoggingOptions>>().Value;

	// ---------- SAFETY ----------

	[Fact]
	public void Default_the_volatile_allowance_to_the_protective_value()
	{
		// The unsafe state must not be reachable by omission — this is the property the gate rests on.
		new AuditLoggingOptions().AllowVolatileAuditStore.ShouldBeFalse();
	}

	[Fact]
	public void Refuse_a_volatile_store_when_the_host_states_no_durability_intention()
	{
		// The fail-closed property, retargeted from the deleted marker to the capability query: a store that
		// does not answer IDurableAuditStore, with AllowVolatile left at its protective default, must refuse
		// startup. RED before the fix (nothing refused it); this is the design's non-vacuity arm.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IAuditStore, FakeVolatileAuditStore>();
		_ = services.AddAuditDurabilityGate();

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(() => Resolve(provider));
	}

	// ---------- LIVENESS ----------

	[Fact]
	public void Start_when_a_durable_store_answers_the_capability_query()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IAuditStore, FakeDurableAuditStore>();
		_ = services.AddAuditDurabilityGate();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider),
			"a durable store answers IDurableAuditStore, so the requirement is satisfied and startup proceeds");
	}

	[Fact]
	public void Start_when_the_host_accepts_a_volatile_store_deliberately()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IAuditStore, FakeVolatileAuditStore>();
		_ = services.AddAuditDurabilityGate();
		_ = services.Configure<AuditLoggingOptions>(o => o.AllowVolatileAuditStore = true);

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider),
			"the gate governs silence, not a deliberate opt-in; an explicit allowance must start");
	}

	[Fact]
	public void Still_resolve_a_working_audit_logger_when_the_gate_passes()
	{
		// Liveness beyond the verdict: the gate must not have broken the thing it guards.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IAuditStore, FakeDurableAuditStore>();
		_ = services.AddAuditLogging();

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IAuditStore>().ShouldBeOfType<FakeDurableAuditStore>();
		using var scope = provider.CreateScope();
		scope.ServiceProvider.GetRequiredService<IAuditLogger>().ShouldNotBeNull();
	}

	// ---------- CAPABILITY QUERY ----------

	[Fact]
	public void Not_answer_the_durability_capability_for_a_volatile_store()
	{
		// Retargeted from the deleted internal marker to the public capability query. A volatile store must
		// answer null so the validator can distinguish it from a durable one.
		IAuditStore volatileStore = new FakeVolatileAuditStore();

		volatileStore.GetService(typeof(IDurableAuditStore)).ShouldBeNull();
	}

	/// <summary>
	/// A stand-in durable store that implements <see cref="IAuditStore" /> <em>and</em>
	/// <see cref="IDurableAuditStore" /> directly, inheriting no first-party base. The default
	/// <c>GetService</c> answers for <see cref="IDurableAuditStore" /> because this instance implements it —
	/// the durable half of the capability query.
	/// </summary>
	private sealed class FakeDurableAuditStore : IAuditStore, IDurableAuditStore
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
			throw new NotSupportedException("Not exercised by the durability gate.");

		public Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken) =>
			Task.FromResult<AuditEvent?>(null);
	}

	/// <summary>
	/// A volatile store: implements <see cref="IAuditStore" /> but NOT <see cref="IDurableAuditStore" />, so
	/// the default <c>GetService</c> answers null for the durability capability. This is the store the gate
	/// must refuse when the host states no durability intention.
	/// </summary>
	private sealed class FakeVolatileAuditStore : IAuditStore
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
			throw new NotSupportedException("Not exercised by the durability gate.");

		public Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken) =>
			Task.FromResult<AuditEvent?>(null);
	}
}
