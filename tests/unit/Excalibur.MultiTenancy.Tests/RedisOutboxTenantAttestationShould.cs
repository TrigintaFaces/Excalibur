// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Redis;

using StackExchange.Redis;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Author-independent lock on the <em>liveness</em> half of the outbox tenant gate for Redis: that a
/// correctly wired Redis outbox is <b>admitted</b> by row-discriminator multi-tenancy, on every connection
/// shape the provider supports.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks.</b> The gate demands <see cref="ITenantPartitionedCapability{TContract}"/> of
/// <see cref="IOutboxStore"/>, and Redis emitted no marker at all, so
/// <c>AddMultiTenancy(RowDiscriminator)</c> threw at startup for a host that was <em>correctly wired</em>.
/// That is a gate rejecting a correct host, not catching a broken one: the safety property held perfectly
/// while the liveness property was broken, which is the failure a suite of refusal-only arms is
/// structurally incapable of seeing.
/// </para>
/// <para>
/// <b>Why Redis belongs on the partitioned seam and not the scoped one.</b> The store reads no ambient
/// tenant on any path. It writes the tenant onto the hash it persists
/// (<c>RedisOutboxStore.SerializeToHashEntries</c>, reached from <c>StageMessageAsync</c>) and hands that
/// value back when the drain rehydrates the message (<c>DeserializeFromHashEntries</c>, reached from the
/// claim path through <c>GetMessagesByIdsAsync</c>), so the owning tenant is re-established from the row.
/// A store filtering on the ambient tenant here would read it as absent at drain time, claim the empty
/// set, and stall delivery for every tenant — while passing any arm that only checks one tenant cannot see
/// another tenant rows.
/// </para>
/// <para>
/// <b>What breaks these arms.</b> The marker is emitted only by <c>AddTenantAwareStore</c>, whose
/// emitter is private, so no provider can register a bare marker and no marker can outlive the
/// registration it attests — a decoupled marker is inexpressible rather than discouraged. The reachable
/// one-token mutation is therefore at the call site, and there are <b>two</b> of them:
/// <c>OutboxBuilderRedisExtensions</c> registers the store on a connection-supplied branch and on a
/// DI-constructed branch, each with its own call. Reverting either to <c>TryAddSingleton</c> reddens
/// exactly that branch shapes and leaves the other green. A fix applied to one alone would leave the
/// other shape rejected while looking complete.
/// </para>
/// <para>
/// <b>Real container, production path.</b> Every arm builds a real <see cref="ServiceProvider"/> through
/// <c>AddExcalibur</c> to <c>AddOutbox</c> to <c>UseRedis</c>. A lock that hand-registered the marker, or
/// that handed the store its dependencies directly, would prove only that the gate reads a marker it was
/// given — precisely how the original lying-marker defect passed a full CI run. No Redis server is
/// required by any arm here: the admitted shapes are never connected.
/// </para>
/// <para>
/// <b>What these arms do not prove, stated rather than implied.</b> That the writes actually populate the
/// discriminator, or that the store serves a tenant-scoped read. That is observable only against real
/// infrastructure and is held by the conformance round-trip, not by a registration-time marker — as the
/// seam contract itself states. Nor do they prove the connection-supplied branch <em>resolves</em>: see
/// <see cref="ResolveTheRedisOutboxUndecorated_OnTheDiConstructedBranch"/> for why that arm is scoped to
/// one branch rather than quietly widened.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class RedisOutboxTenantAttestationShould
{
	/// <summary>Never opened: no admitted arm constructs a store on a connection-supplied shape.</summary>
	private const string UnusedConnectionString = "localhost:6379";

	// ---- LIVENESS: the real gate ADMITS the real provider. This is the bead. ---------------------------

	[Theory]
	[InlineData(RedisConnectionShape.ConnectionString)]
	[InlineData(RedisConnectionShape.MultiplexerFactory)]
	[InlineData(RedisConnectionShape.DiConstructed)]
	public void AdmitTheRealRedisOutbox_UnderRowDiscriminator_ForEveryConnectionShape(
		RedisConnectionShape shape)
	{
		var services = BuildRedisOutboxHost(shape);

		// Reaching past this line is the assertion. Before the fix this threw, so a consumer on Redis could
		// not turn on row-discriminator multi-tenancy at all.
		Should.NotThrow(
			() => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
			"RowDiscriminator must ADMIT a correctly wired Redis outbox. This provider carries the tenant on "
			+ "the hash it persists and re-establishes it on drain, which is exactly the mechanism "
			+ "ITenantPartitionedCapability attests. Rejecting it is the gate refusing a correct host, and "
			+ "that is invisible to every refusal-only arm on this contract.");
	}

	[Theory]
	[InlineData(RedisConnectionShape.ConnectionString)]
	[InlineData(RedisConnectionShape.MultiplexerFactory)]
	[InlineData(RedisConnectionShape.DiConstructed)]
	public void AttestRowPartitionedTenancy_ForEveryRedisConnectionShape(RedisConnectionShape shape)
	{
		using var provider = BuildRedisOutboxHost(shape).BuildServiceProvider();

		_ = provider.GetRequiredService<ITenantPartitionedCapability<IOutboxStore>>().ShouldNotBeNull(
			"The Redis outbox must present ITenantPartitionedCapability<IOutboxStore>, emitted by "
			+ "AddTenantAwareStore inseparably from the store registration. Without it every host "
			+ "using this provider is refused by RowDiscriminator. Both registration branches must emit it: "
			+ "a marker on only one leaves the other connection shape rejected while looking fixed.");
	}

	[Fact]
	public async Task ResolveTheRedisOutboxUndecorated_OnTheDiConstructedBranch()
	{
		// Scoped to the DI-constructed branch deliberately, and the reason is a property of the provider
		// rather than an omission. On the connection-supplied branch the store demands a CONCRETE
		// ConnectionMultiplexer: the connection-string shape would dial a real server to produce one, and
		// the factory shape casts whatever IConnectionMultiplexer is registered to the concrete type, which
		// a stand-in cannot satisfy. Constructing the store on those shapes therefore needs real
		// infrastructure, and substituting a mock is the exact shape that let a decoupled marker pass CI
		// here. Those branches are held by the admission and marker arms above; this arm is not widened to
		// cover them by pretending.
		var services = BuildRedisOutboxHost(RedisConnectionShape.DiConstructed);
		_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		// The DI-constructed branch selects the options-only constructor, which validates and stores its
		// inputs without dialing. Supplied post-hoc so the shape stays on the no-connection branch: setting
		// a connection string through the builder would route registration to the other branch entirely.
		_ = services.PostConfigure<RedisOutboxOptions>(static o =>
		{
			o.ConnectionString = UnusedConnectionString;
			o.KeyPrefix = "outbox";
		});

		// Disposed asynchronously: this arm actually constructs the store, and the store is
		// IAsyncDisposable-only, so a synchronous container dispose throws.
		await using var provider = services.BuildServiceProvider();

		// Resolved through the real container, not asserted from a descriptor: a registration that cannot be
		// constructed satisfies a descriptor scan and still fails the consumer at runtime.
		var store = provider.GetRequiredKeyedService<IOutboxStore>("redis");

		_ = store.ShouldBeOfType<RedisOutboxStore>(
			"The outbox must resolve as the provider own store. A tenant-scoping wrapper on this contract "
			+ "would read the ambient tenant as absent at drain time, claim the empty set, and stall "
			+ "delivery for every tenant while looking safe.");

		// The keyed default alias is what a host without an explicit key resolves, so it must reach the same
		// admitted store rather than a second, unattested registration.
		provider.GetRequiredKeyedService<IOutboxStore>("default").ShouldBeSameAs(
			store,
			"The default outbox alias must resolve to the same admitted store instance.");
	}

	// ---- SAFETY: it attests the mechanism it has, and not the one it does not ---------------------------

	[Theory]
	[InlineData(RedisConnectionShape.ConnectionString)]
	[InlineData(RedisConnectionShape.MultiplexerFactory)]
	[InlineData(RedisConnectionShape.DiConstructed)]
	public void NotAttestAmbientTenantScoping_ForEveryRedisConnectionShape(RedisConnectionShape shape)
	{
		using var provider = BuildRedisOutboxHost(shape).BuildServiceProvider();

		provider.GetService<ITenantScopingCapability<IOutboxStore>>().ShouldBeNull(
			"The Redis outbox must not present ITenantScopingCapability<IOutboxStore>. That marker attests "
			+ "the store applies the ambient tenant discriminator to every operation, and this store reads "
			+ "no ambient tenant on any path. Presenting it is the lying-marker defect: the gate passes and "
			+ "the documentation then describes a verification that did not happen.");
	}

	// ---- SAFETY: the gate still has teeth, and it has them against a KEYED registration -----------------

	[Fact]
	public void RejectAKeyedRedisOutbox_ThatAttestsNothing()
	{
		var services = new ServiceCollection();

		// Redis registers IOutboxStore KEYED, never as a plain service type. If the gate descriptor scan
		// did not see keyed registrations it would silently never fire for this provider — an ungated
		// outbox rather than a refused one — and every admission arm above would pass for the wrong reason.
		// This arm is what establishes that the gate reaches the registration shape Redis actually uses.
		_ = services.AddKeyedSingleton("redis", (IServiceProvider _, object? _) => A.Fake<IOutboxStore>());

		var thrown = Should.Throw<InvalidOperationException>(
			() => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
			"RowDiscriminator must reject a KEYED outbox registration that proves no tenant capability. If "
			+ "this does not throw, the gate does not see keyed descriptors, and the admission arms above "
			+ "are vacuous: they would pass against a provider that attests nothing at all.");

		thrown.Message.ShouldContain(
			nameof(IOutboxStore),
			Case.Sensitive,
			"The rejection must name the contract that failed, or a consumer cannot act on it.");
	}

	/// <summary>
	/// The connection shapes <c>UseRedis</c> supports. The first two take the connection-supplied
	/// registration branch; the third takes the DI-constructed branch. Each branch has its own registration
	/// call, so both must attest independently.
	/// </summary>
	public enum RedisConnectionShape
	{
		/// <summary>Configured with a connection string; the store is built from a dialed multiplexer.</summary>
		ConnectionString,

		/// <summary>Configured with an <see cref="IConnectionMultiplexer"/> factory resolved from DI.</summary>
		MultiplexerFactory,

		/// <summary>Configured with neither; the store is constructed by the container from options alone.</summary>
		DiConstructed,
	}

	/// <summary>Wires a host through the production Redis outbox registration path for the given shape.</summary>
	private static ServiceCollection BuildRedisOutboxHost(RedisConnectionShape shape)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(A.Fake<IConnectionMultiplexer>());
		_ = services.AddExcalibur(x => x.AddOutbox(outbox => outbox.UseRedis(redis =>
		{
			_ = redis.KeyPrefix("outbox");
			switch (shape)
			{
				case RedisConnectionShape.ConnectionString:
					_ = redis.ConnectionString(UnusedConnectionString);
					break;
				case RedisConnectionShape.MultiplexerFactory:
					_ = redis.ConnectionMultiplexerFactory(
						static sp => sp.GetRequiredService<IConnectionMultiplexer>());
					break;
				case RedisConnectionShape.DiConstructed:
				default:
					break;
			}
		})));

		return services;
	}
}
