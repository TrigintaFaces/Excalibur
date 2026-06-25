// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Consumer-override regression tests for Sprint 794 Workstream D1
/// (<c>bd-ffecs4</c>): proves that (a) consumer pre-registrations of core
/// Dispatch singletons survive a subsequent <c>AddDispatch</c> call
/// (<c>TryAdd</c> first-wins semantics) and (b) a second
/// <c>AddDispatch(configure)</c> invocation is a no-op and does NOT invoke the
/// second configure lambda.
/// </summary>
/// <remarks>
/// Complements the <c>AddDispatchBridgeMinimalWiringConformanceTests</c>
/// idempotence pin — that test proves the descriptor-count invariant; these
/// tests prove the consumer-override invariant. Both properties must hold per
/// COMPASS msg 1480 §Consumer-override test surface.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "DI-IDEMPOTENCE")]
public sealed class AddDispatchIdempotenceShould
{
	[Fact]
	public void ConsumerSingletonBeforeAddDispatch_CustomMiddlewareApplicabilityStrategyWins()
	{
		var services = new ServiceCollection();
		var custom = new FakeMiddlewareApplicabilityStrategy();

		_ = services.AddSingleton<IMiddlewareApplicabilityStrategy>(custom);
		_ = services.AddDispatch();

		using var sp = services.BuildServiceProvider();
		var resolved = sp.GetRequiredService<IMiddlewareApplicabilityStrategy>();

		resolved.ShouldBeSameAs(custom);
	}

	[Fact]
	public void ConsumerSingletonBeforeAddDispatch_CustomPipelineProfileRegistryWins()
	{
		// PipelineProfileRegistry is internal; use the IPipelineProfileRegistry
		// abstraction (its public contract) as the override surface.
		var services = new ServiceCollection();
		var custom = new FakePipelineProfileRegistry();

		_ = services.AddSingleton<IPipelineProfileRegistry>(custom);
		_ = services.AddDispatch(static _ => { });

		using var sp = services.BuildServiceProvider();
		var resolved = sp.GetRequiredService<IPipelineProfileRegistry>();

		resolved.ShouldBeSameAs(custom);
	}

	[Fact]
	public void ConsumerTypeRegistrationBeforeAddDispatch_CustomPipelineProfileRegistryWins()
	{
		// txmwh9 (Reviewer 15438, SA 15441): the guard whitelists ONLY the framework-default registration
		// (ImplementationType == typeof(PipelineProfileRegistry) && ImplementationInstance is null && no factory),
		// so a consumer's TYPE-registration override must also survive AddDispatch — not just an instance form.
		// RED on the unconditional Replace (it clobbers the type-reg too); this fact RED-proves the discriminator
		// is ImplementationType-based, not instance-presence-based (an instance-only guard would re-clobber this).
		var services = new ServiceCollection();
		_ = services.AddSingleton<IPipelineProfileRegistry, FakePipelineProfileRegistry>();

		_ = services.AddDispatch(static _ => { });

		using var sp = services.BuildServiceProvider();
		_ = sp.GetRequiredService<IPipelineProfileRegistry>().ShouldBeOfType<FakePipelineProfileRegistry>();
	}

	[Fact]
	public void ConsumerFactoryRegistrationBeforeAddDispatch_CustomPipelineProfileRegistryWins()
	{
		// txmwh9 (Reviewer 15438): a consumer FACTORY registration exposes no instance at ctor-time, so a guard
		// keyed on ImplementationInstance-presence would still clobber it. The framework-default whitelist leaves
		// it untouched. RED on the unconditional Replace; GREEN on the guarded fix — the third override shape.
		var services = new ServiceCollection();
		_ = services.AddSingleton<IPipelineProfileRegistry>(static _ => new FakePipelineProfileRegistry());

		_ = services.AddDispatch(static _ => { });

		using var sp = services.BuildServiceProvider();
		_ = sp.GetRequiredService<IPipelineProfileRegistry>().ShouldBeOfType<FakePipelineProfileRegistry>();
	}

	[Fact]
	public void NoConsumerOverride_BuilderConfiguredRegistryRemainsAuthoritative()
	{
		// txmwh9 paired guard (SA 15429): with NO consumer-provided IPipelineProfileRegistry, the builder's
		// configured registry MUST stay authoritative at resolve time — i.e. K rb4g4b's registry-split fix must
		// remain intact. The default profile (self-registered by the PipelineProfileRegistry ctor) must resolve,
		// so the default pipeline (OutboxStaging) still runs. This is the lock that stops a future txmwh9 fix
		// from re-opening K while satisfying S794 — mechanism-independent (holds under guarded-Replace AND adopt).
		var services = new ServiceCollection();

		_ = services.AddDispatch(static _ => { });

		using var sp = services.BuildServiceProvider();
		var registry = sp.GetRequiredService<IPipelineProfileRegistry>();

		_ = registry.GetProfile(DefaultPipelineProfiles.Default).ShouldNotBeNull(
			"with no consumer override the builder-configured registry must be authoritative and expose the " +
			"default profile (K rb4g4b registry-split fix must not regress)");
	}

	[Fact]
	public void SecondAddDispatchConfigure_ConfigureNotInvoked()
	{
		var services = new ServiceCollection();
		var firstConfigureInvoked = 0;
		var secondConfigureInvoked = 0;

		_ = services.AddDispatch(dispatch =>
		{
			firstConfigureInvoked++;
			_ = dispatch;
		});

		_ = services.AddDispatch(dispatch =>
		{
			secondConfigureInvoked++;
			throw new InvalidOperationException(
				"Second AddDispatch configure lambda must not be invoked — " +
				"the idempotence guard should fire before the builder runs.");
		});

		firstConfigureInvoked.ShouldBe(1);
		secondConfigureInvoked.ShouldBe(0);
	}

	[Fact]
	public void IdempotenceAcrossDoubleBuildScenario_DescriptorCountStable()
	{
		var services = new ServiceCollection();

		_ = services.AddDispatch(static dispatch => dispatch.ConfigurePipeline("default", static _ => { }));
		var nonBenignAfterFirst = CountNonBenignDescriptors(services);

		_ = services.AddDispatch(static dispatch => dispatch.ConfigurePipeline("unused", static _ => { }));
		var nonBenignAfterSecond = CountNonBenignDescriptors(services);

		nonBenignAfterSecond.ShouldBe(nonBenignAfterFirst,
			$"Second AddDispatch(configure) must not register any non-benign descriptors " +
			$"(IConfigureOptions / IPostConfigureOptions / IValidateOptions are benign options-pipeline " +
			$"additions per the MinimalWiring benign-drift whitelist). " +
			$"First-call non-benign count={nonBenignAfterFirst}, second-call non-benign count={nonBenignAfterSecond}.");
	}

	private static int CountNonBenignDescriptors(IServiceCollection services)
	{
		var count = 0;
		foreach (var descriptor in services)
		{
			if (!IsBenignOptionsDescriptor(descriptor))
			{
				count++;
			}
		}
		return count;
	}

	private static bool IsBenignOptionsDescriptor(ServiceDescriptor descriptor)
	{
		var svcType = descriptor.ServiceType;
		if (!svcType.IsGenericType)
		{
			return false;
		}
		var def = svcType.GetGenericTypeDefinition();
		return def == typeof(Microsoft.Extensions.Options.IConfigureOptions<>)
			|| def == typeof(Microsoft.Extensions.Options.IPostConfigureOptions<>)
			|| def == typeof(Microsoft.Extensions.Options.IValidateOptions<>);
	}

	private sealed class FakeMiddlewareApplicabilityStrategy : IMiddlewareApplicabilityStrategy
	{
		public MessageKinds DetermineMessageKinds<T>(T message) where T : IDispatchMessage => MessageKinds.None;

		public bool ShouldApplyMiddleware(MessageKinds applicableKinds, MessageKinds messageKinds) => true;
	}

	private sealed class FakePipelineProfileRegistry : IPipelineProfileRegistry
	{
		public IPipelineProfile? GetProfile(string profileName) => null;

		public void RegisterProfile(IPipelineProfile profile) { }

		public IEnumerable<string> GetProfileNames() => [];

		public bool RemoveProfile(string profileName) => false;

		public void SetDefaultProfile(string profileName) { }
	}
}