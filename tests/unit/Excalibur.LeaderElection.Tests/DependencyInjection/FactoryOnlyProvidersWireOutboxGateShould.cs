// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.LeaderElection.Tests.DependencyInjection;

/// <summary>
/// Regression lock: every provider registration path a consumer can reach must leave the outbox leader
/// gate resolvable, and must leave a host that opts out of fencing able to start and drain.
/// </summary>
/// <remarks>
/// <para>
/// THE BUG. Seven leader-election providers exist. Four of them (Postgres, SqlServer, Redis, MongoDB)
/// register an <c>ILeaderElection</c> - keyed under the provider name, keyed under <c>"default"</c>, and
/// unkeyed. Three of them (Consul, Kubernetes, InMemory) registered <em>only</em> an
/// <c>ILeaderElectionFactory</c> and never an <c>ILeaderElection</c> at all. The outbox leader gate is
/// constructed from <c>GetRequiredService&lt;ILeaderElection&gt;()</c>, so on those three the gate could
/// not be built: a consumer who registered a real distributed election got an unresolvable gate instead
/// of a fenced drain.
/// </para>
/// <para>
/// The reach is wider than the three provider packages, because the gate arrives from the caller rather
/// than from the provider. <c>AddLeaderElection(le =&gt; le.UseConsul(...))</c> registers the gate
/// unconditionally and aliases the unkeyed <c>ILeaderElection</c> onto the keyed <c>"default"</c> one, so
/// a provider that registers no keyed <c>"default"</c> election breaks the documented builder path too,
/// not merely its own standalone entry point. The startup prerequisite validator names
/// <c>le.UseInMemory()</c> in its own remedy text, and that call did not satisfy it.
/// </para>
/// <para>
/// The assertion is on the RESOLVED gate and the RESOLVED election, never on a descriptor count: a
/// registration that cannot be satisfied from the container is not a wiring. Resolving the gate resolves
/// the election behind it, so a gate registered against an election that exists only under a key - the
/// exact shape of this bug - fails here rather than at first use in production.
/// </para>
/// <para>
/// SAFETY arms prove the gate resolves. They are paired with LIVENESS arms because a gate that resolves
/// and then reports <c>ShouldProcess == false</c> forever satisfies every safety assertion while draining
/// nothing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class FactoryOnlyProvidersWireOutboxGateShould
{
	// Consul's client is constructed against this address but never connects: the election opens its
	// session when it campaigns, not when the container builds it.
	private const string ConsulAddress = "http://localhost:8500";

	public static TheoryData<string> ProviderPaths() => new("consul", "kubernetes", "inmemory");

	[Theory]
	[MemberData(nameof(ProviderPaths))]
	public async Task ResolveTheOutboxLeaderGate_WhenAProviderIsAddedThroughTheBuilderPath(string provider)
	{
		// SAFETY - THE BUG ARM (builder path). AddLeaderElection registers the gate unconditionally, so a
		// provider that registers no ILeaderElection leaves a gate that cannot be constructed.
		// The resolved election is IAsyncDisposable only, so the container must be disposed asynchronously.
		await using var services = BuildBuilderPathProvider(provider);

		_ = Should.NotThrow(
			() => services.GetRequiredService<ILeaderElection>(),
			$"UseX() for '{provider}' must make an unkeyed ILeaderElection resolvable. The outbox leader " +
			"gate is built from GetRequiredService<ILeaderElection>(), so a factory-only registration " +
			"leaves the gate unsatisfiable and the fencing guarantee undeliverable.");

		_ = Should.NotThrow(
			() => services.GetRequiredService<ILeaderProcessingGate>(),
			$"The outbox leader gate must actually be constructible on the '{provider}' builder path. A " +
			"registered-but-unsatisfiable gate is an unfenced drain wearing a passing registration check.");
	}

	[Theory]
	[MemberData(nameof(ProviderPaths))]
	public async Task SatisfyTheStartupPrerequisiteValidator_WhenAProviderIsAddedThroughTheBuilderPath(string provider)
	{
		// SAFETY. The prerequisite validator refuses host startup unless a keyed "default" ILeaderElection
		// resolves, and its own remedy text names le.UseInMemory() as a fix. That call must satisfy it.
		await using var services = BuildBuilderPathProvider(provider);

		services.GetKeyedService<ILeaderElection>("default").ShouldNotBeNull(
			$"UseX() for '{provider}' must register the keyed \"default\" ILeaderElection that " +
			"LeaderElectionPrerequisiteValidator requires, or the host refuses to start while the consumer " +
			"has done exactly what the validator's own error message told them to do.");
	}

	[Theory]
	[MemberData(nameof(ProviderPaths))]
	public async Task ResolveTheOutboxLeaderGate_WhenAProviderIsAddedThroughItsStandaloneEntryPoint(string provider)
	{
		// SAFETY - THE BUG ARM (standalone path). The store-specific Add* overloads are a documented entry
		// point in their own right, not only a step inside the builder.
		await using var services = BuildStandalonePathProvider(provider);

		_ = Should.NotThrow(
			() => services.GetRequiredService<ILeaderElection>(),
			$"The standalone Add* entry point for '{provider}' must make an unkeyed ILeaderElection " +
			"resolvable, the way the Postgres and SqlServer standalone paths already do.");

		_ = Should.NotThrow(
			() => services.GetRequiredService<ILeaderProcessingGate>(),
			$"The standalone Add* entry point for '{provider}' must leave the outbox leader gate " +
			"constructible, or a consumer who registered an election still drains unfenced.");
	}

	[Fact]
	public async Task PermitProcessingOnceLeadershipIsHeld_WhenTheGateIsResolvedFromAProviderPath()
	{
		// LIVENESS. A gate that resolves and then refuses forever passes every safety arm above while
		// draining nothing. This drives one real acquisition through the in-process election and asserts
		// the gate actually opens - the guarantee is "fenced", not "stopped".
		await using var services = BuildStandalonePathProvider("inmemory");

		var gate = services.GetRequiredService<ILeaderProcessingGate>();
		gate.ShouldProcess.ShouldBeFalse(
			"Before any leadership is acquired the gate must fail closed - it gates on a leadership " +
			"snapshot, never on an advisory bool.");

		var election = services.GetRequiredService<ILeaderElection>();
		await election.StartAsync(TestContext.Current.CancellationToken);

		await WaitUntilAsync(() => election.CurrentLeadership is not null, TimeSpan.FromSeconds(5));

		gate.ShouldProcess.ShouldBeTrue(
			"Once this instance holds leadership the gate must open. A gate wired so that it never opens " +
			"would satisfy every 'the drain is fenced' assertion while the outbox never drains at all.");

		await election.StopAsync(TestContext.Current.CancellationToken);
	}

	[Fact]
	public async Task LeaveTheSingleWriterOptOutIntact_WhenAProviderAlsoRegistersTheGate()
	{
		// LIVENESS. Fencing is default-on, so the opt-out is the only way a genuinely single-writer host
		// keeps draining. Registering the gate by default must not clobber it.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryLeaderElection();
		_ = services.Configure<OutboxDeliveryOptions>(o => o.SingleActiveWriter = true);

		await using var built = services.BuildServiceProvider(validateScopes: false);

		built.GetRequiredService<IOptions<OutboxDeliveryOptions>>().Value.SingleActiveWriter.ShouldBeTrue(
			"AsSingleWriter() sets SingleActiveWriter, and a provider that now registers the gate by " +
			"default must leave that opt-out standing. A host that opted out must still start and drain.");

		_ = Should.NotThrow(
			() => built.GetRequiredService<ILeaderElection>(),
			"The opt-out concerns the drain, not the election: the election must still resolve.");
	}

	private static ServiceProvider BuildBuilderPathProvider(string provider)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddExcaliburLeaderElection(builder =>
		{
			switch (provider)
			{
				case "consul":
					_ = builder.UseConsul(c => c.Address(ConsulAddress).LockKey("excalibur-tests").ResourceName("excalibur-tests-leader"));
					break;
				case "kubernetes":
					_ = builder.UseKubernetes(k => k.LeaseName("excalibur-tests-leader"));
					break;
				default:
					_ = builder.UseInMemory();
					break;
			}
		});

		// The gate is registered by the caller of the builder (AddLeaderElection), not by UseX(). Registering
		// it here reproduces that composition without pulling in the whole Excalibur host.
		OutboxBuilderLeaderElectionExtensions.RegisterOutboxLeaderGate(services);

		SubstituteKubernetesClient(services, provider);

		return services.BuildServiceProvider(validateScopes: false);
	}

	private static ServiceProvider BuildStandalonePathProvider(string provider)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		switch (provider)
		{
			case "consul":
				_ = services.AddConsulLeaderElection(c => c.Address(ConsulAddress).LockKey("excalibur-tests").ResourceName("excalibur-tests-leader"));
				break;
			case "kubernetes":
				_ = services.AddExcaliburKubernetesLeaderElection(k => k.LeaseName("excalibur-tests-leader"));
				break;
			default:
				_ = services.AddInMemoryLeaderElection();
				break;
		}

		SubstituteKubernetesClient(services, provider);

		return services.BuildServiceProvider(validateScopes: false);
	}

	// The Kubernetes registration builds a real client from in-cluster config or a kubeconfig file, neither
	// of which exists on a build agent. Registering the substitute AFTER the provider makes it the winning
	// descriptor. These arms assert DI wiring, not Kubernetes behaviour.
	private static void SubstituteKubernetesClient(IServiceCollection services, string provider)
	{
		if (provider is "kubernetes")
		{
			_ = services.AddSingleton(A.Fake<IKubernetes>());
		}
	}

	private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
	{
		var deadline = DateTimeOffset.UtcNow + timeout;
		while (DateTimeOffset.UtcNow < deadline)
		{
			if (condition())
			{
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
		}
	}
}
