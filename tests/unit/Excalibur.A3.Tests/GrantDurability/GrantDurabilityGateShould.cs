// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Authorization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Tests.GrantDurability;

/// <summary>
/// Binds the fail-closed contract for authorization-grant durability.
/// </summary>
/// <remarks>
/// The failure this prevents is quiet in an unusual way. A lost grant set does not break authorization —
/// it makes authorization deny everyone, because a user whose grants vanished is indistinguishable from a
/// user who never had any. The system stays up and stops letting anyone in, having reported every grant
/// as saved.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class GrantDurabilityGateShould
{
	// ---------- SAFETY ----------

	[Fact]
	public void Refuse_the_volatile_default_when_the_host_says_nothing()
	{
		using var provider = new ServiceCollection().AddGrantDurabilityGate().BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(() => Resolve(provider));
	}

	[Fact]
	public void Say_what_is_lost_and_name_both_remedies()
	{
		using var provider = new ServiceCollection().AddGrantDurabilityGate().BuildServiceProvider();

		var error = Should.Throw<OptionsValidationException>(() => Resolve(provider));

		error.Message.ShouldContain("deny");
		error.Message.ShouldContain(nameof(GrantDurabilityOptions.AllowVolatileGrantStore));
	}

	[Fact]
	public void Default_the_volatile_allowance_to_the_protective_value() =>
		new GrantDurabilityOptions().AllowVolatileGrantStore.ShouldBeFalse();

	// ---------- LIVENESS ----------

	[Fact]
	public void Start_when_a_durable_store_is_registered_through_the_attesting_seam()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton<IGrantStore, FakeDurableGrantStore>();
		_ = services.AddGrantDurabilityGate();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
	}

	[Fact]
	public void Start_when_the_host_accepts_a_volatile_store_deliberately()
	{
		var services = new ServiceCollection();
		_ = services.AddGrantDurabilityGate();
		_ = services.Configure<GrantDurabilityOptions>(o => o.AllowVolatileGrantStore = true);

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
	}

	// ---------- PRODUCTION-PATH WIRING ----------



	[Fact]
	public void Not_answer_the_durability_capability_for_a_volatile_store()
	{
		// Retargeted from the deleted internal marker to the public capability query: a volatile grant
		// store must answer null so the validator can distinguish it from a durable one.
		IGrantStore volatileStore = new FakeVolatileGrantStore();

		volatileStore.GetService(typeof(IDurableGrantStore)).ShouldBeNull();
	}

	private static GrantDurabilityOptions Resolve(IServiceProvider provider) =>
		provider.GetRequiredService<IOptions<GrantDurabilityOptions>>().Value;

	/// <summary>
	/// A durable store: implements <see cref="IGrantStore" /> <em>and</em> <see cref="IDurableGrantStore" />
	/// directly, inheriting no first-party base. It answers for the durability capability through the
	/// default <c>GetService</c> because it implements the marker.
	/// </summary>
	private sealed class FakeDurableGrantStore : IGrantStore, IDurableGrantStore
	{
		public Task<Grant?> GetGrantAsync(string userId, string tenantId, string grantType,
			string qualifier, CancellationToken cancellationToken) => Task.FromResult<Grant?>(null);

		public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<Grant>>([]);

		public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, bool includeExpired,
			CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Grant>>([]);

		public Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken) => Task.FromResult(1);

		public Task<int> DeleteGrantAsync(string userId, string tenantId, string grantType,
			string qualifier, string? revokedBy, DateTimeOffset? revokedOn,
			CancellationToken cancellationToken) => Task.FromResult(1);

		public Task<bool> GrantExistsAsync(string userId, string tenantId, string grantType,
			string qualifier, CancellationToken cancellationToken) => Task.FromResult(false);

		// Durable: answer for the marker this instance implements. Deferring to the default GetService
		// (IsInstanceOfType) would also work; returning this explicitly makes the capability unmistakable.
		public object? GetService(Type serviceType) =>
			serviceType.IsInstanceOfType(this) ? this : null;
	}

	/// <summary>
	/// A volatile store: implements <see cref="IGrantStore" /> but NOT <see cref="IDurableGrantStore" />,
	/// so it answers null for the durability capability. This is the store the gate must refuse.
	/// </summary>
	private sealed class FakeVolatileGrantStore : IGrantStore
	{
		public Task<Grant?> GetGrantAsync(string userId, string tenantId, string grantType,
			string qualifier, CancellationToken cancellationToken) => Task.FromResult<Grant?>(null);

		public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<Grant>>([]);

		public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, bool includeExpired,
			CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Grant>>([]);

		public Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken) => Task.FromResult(1);

		public Task<int> DeleteGrantAsync(string userId, string tenantId, string grantType,
			string qualifier, string? revokedBy, DateTimeOffset? revokedOn,
			CancellationToken cancellationToken) => Task.FromResult(1);

		public Task<bool> GrantExistsAsync(string userId, string tenantId, string grantType,
			string qualifier, CancellationToken cancellationToken) => Task.FromResult(false);

		public object? GetService(Type serviceType) => null;
	}
}
