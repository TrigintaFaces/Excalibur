// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
	// ---------- THE STANDALONE-CORE PATH ----------
	//
	// These two are the pair the ruling requires, and they are a pair on purpose: the safety arm alone is
	// satisfied by a gate that never installs, and the liveness arm alone is satisfied by one that always
	// refuses. What they bind together is that the verb, and only the verb, decides.
	//
	// The defect they close: GrantDurabilityOptions is public and shipped from A3.Core, and its own XML doc
	// promises a fail-fast startup check — but the only thing that installed the gate lived in the sibling
	// A3 package. A consumer on the documented standalone path set the option, read the doc, and got
	// nothing.

	/// <summary>
	/// LIVENESS: a host wiring ONLY A3.Core and calling the verb refuses to start on a volatile store.
	/// </summary>
	/// <remarks>
	/// This is the arm that would have caught the original defect, and it fails against pre-fix source by
	/// construction: before the fix there was no <c>RequireDurableGrants</c> to call, so it does not compile.
	/// </remarks>
	[Fact]
	public void Refuse_to_start_on_the_standalone_core_path_when_the_verb_is_called()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburA3Core().RequireDurableGrants();

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(
			() => Resolve(provider),
			"the standalone Core host asked for durable grants and the fallback store is volatile");
	}

	/// <summary>
	/// SAFETY: the same host that does NOT call the verb still starts.
	/// </summary>
	/// <remarks>
	/// Without this, installing the gate unconditionally on Core would pass the arm above while breaking
	/// every existing standalone host — including the shipped StandaloneA3 sample, which calls
	/// <c>AddExcaliburA3Core()</c> and then builds. Opt-in means opt-in.
	/// </remarks>
	[Fact]
	public void Still_start_on_the_standalone_core_path_when_the_verb_is_not_called()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburA3Core();

		using var provider = services.BuildServiceProvider();

		// The property is that NO durability validation was installed, and it is asserted directly rather
		// than through the options value. My first version of this arm resolved IOptions<GrantDurabilityOptions>
		// and asserted it did not throw OptionsValidationException -- which fails for an unrelated reason:
		// without the verb the option is never registered at all, so the resolve throws
		// InvalidOperationException instead. That probe tested "is the option resolvable", not "is the gate
		// installed", and the two answers differ exactly here.
		provider.GetService<IValidateOptions<GrantDurabilityOptions>>().ShouldBeNull(
			"the host never asked for durable grants, so nothing may refuse its startup");
	}

	/// <summary>
	/// LIVENESS, the other direction: the verb does not refuse a host that HAS a durable store.
	/// </summary>
	/// <remarks>
	/// Without this, "refuse whenever the verb is called" passes the first arm. The gate must discriminate
	/// on the store, not on having been asked.
	/// </remarks>
	[Fact]
	public void Start_when_the_verb_is_called_and_the_grant_store_is_durable()
	{
		var services = new ServiceCollection();
		services.AddSingleton<IGrantStore, FakeDurableGrantStore>();
		_ = services.AddExcaliburA3Core().RequireDurableGrants();

		using var provider = services.BuildServiceProvider();

		_ = Should.NotThrow(() => Resolve(provider));
	}

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
	//
	// Every arm above invokes the gate DIRECTLY. That proves the gate works when called and says nothing
	// about whether anything calls it -- so the evidence that the production entry point is protected was a
	// source read (one grep hit at A3ServiceCollectionExtensions.cs). A source read is sound today and it is
	// not a lock: drop that one line in a refactor and every arm above still passes while every host
	// composed through AddExcaliburA3() silently accepts a volatile grant store. These three arms bind
	// REACHABILITY: they go through the public registration call a consumer actually writes, and never name
	// the gate.

	[Fact]
	public void Refuse_a_volatile_store_through_the_PUBLIC_full_stack_registration()
	{
		// SAFETY, and the arm the source read could not provide. Nothing here mentions the gate; the only
		// thing under test is that composing A3 the documented way leaves a host unable to start on a
		// volatile grant store.
		var services = new ServiceCollection();
		_ = services.AddExcaliburA3();

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(
			() => Resolve(provider),
			"AddExcaliburA3() is the production composition; a host that registers no durable grant store "
			+ "and opts out of nothing must fail at startup rather than deny every user after a restart");
	}

	[Fact]
	public void Start_through_the_PUBLIC_full_stack_registration_when_a_durable_store_is_present()
	{
		// LIVENESS. Without this, the arm above is satisfied by a composition that refuses EVERY
		// configuration -- which is the cheapest way to look safe and the most expensive way to be wrong.
		// The durable store is registered BEFORE the composition because the in-memory default goes in
		// through TryAdd, so first registration wins and this is what a consumer's UseGrantStore() achieves.
		var services = new ServiceCollection();
		_ = services.AddSingleton<IGrantStore, FakeDurableGrantStore>();
		_ = services.AddExcaliburA3();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
	}

	[Fact]
	public void Start_through_the_PUBLIC_full_stack_registration_when_the_host_opts_out_deliberately()
	{
		// LIVENESS. The documented escape hatch has to survive the composition too: a host that has read
		// what volatile grants cost and accepts them must still be able to run.
		var services = new ServiceCollection();
		_ = services.AddExcaliburA3();
		_ = services.Configure<GrantDurabilityOptions>(static o => o.AllowVolatileGrantStore = true);

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
	}

	[Fact]
	public void Not_answer_the_durability_capability_for_a_volatile_store()
	{
		// Retargeted from the deleted internal marker to the public capability query: a volatile grant
		// store must answer null so the validator can distinguish it from a durable one.
		IGrantStore volatileStore = new FakeVolatileGrantStore();

		volatileStore.GetService(typeof(IDurableGrantStore)).ShouldBeNull();
	}

	[Fact]
	public void Refuse_a_store_that_answers_the_capability_query_with_the_wrong_type()
	{
		// SAFETY. The guard asks the store for IDurableGrantStore. A store that answers with SOMETHING
		// rather than with the capability is not durable, and a guard testing the reply for non-null
		// cannot tell the two apart -- it reports the capability present and the host starts without it.
		// This is reachable without a malicious store: a hand-written GetService returning `this` or a
		// cached object unconditionally has the effect, and so does a test double that proxies every type.
		var services = new ServiceCollection();
		_ = services.AddSingleton<IGrantStore, FakeOverAnsweringGrantStore>();
		_ = services.AddGrantDurabilityGate();

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(
			() => Resolve(provider),
			"a store answering the durability query with an object of the wrong type is NOT durable, and "
			+ "the gate must refuse it exactly as it refuses a store that answers null");
	}

	[Fact]
	public void Answer_the_capability_query_with_something_non_null_but_wrong_in_the_over_answering_double()
	{
		// Keeps the arm above honest. If the double ever stopped over-answering, the arm would pass
		// because the store looks volatile rather than because the guard checks the type.
		IGrantStore overAnswering = new FakeOverAnsweringGrantStore();

		var answer = overAnswering.GetService(typeof(IDurableGrantStore));

		answer.ShouldNotBeNull("the double exists to answer non-null for a capability it does not have");
		answer.ShouldNotBeAssignableTo<IDurableGrantStore>();
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

	/// <summary>
	/// A store that answers every capability query with a non-null object of the wrong type.
	/// </summary>
	/// <remarks>
	/// This is the subject a non-null test cannot distinguish from a durable store. It implements
	/// <see cref="IGrantStore" /> only -- it is NOT durable -- and returns a
	/// <see cref="System.Text.StringBuilder" /> for anything asked of it.
	/// </remarks>
	private sealed class FakeOverAnsweringGrantStore : IGrantStore
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

		// The defect this arm exists for: non-null for everything, correct for nothing.
		public object? GetService(Type serviceType) => new System.Text.StringBuilder();
	}
}
