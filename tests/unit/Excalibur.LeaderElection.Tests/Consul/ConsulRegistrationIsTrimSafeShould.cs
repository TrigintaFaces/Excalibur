// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.LeaderElection.Consul;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

namespace Excalibur.LeaderElection.Tests.Consul;

/// <summary>
/// Locks the Consul registration entry points as trim- and native-AOT-safe, and locks the replacement
/// route for configuration binding.
/// </summary>
/// <remarks>
/// <para>
/// Both entry points used to carry <see cref="RequiresUnreferencedCodeAttribute"/> and
/// <see cref="RequiresDynamicCodeAttribute"/> for one reason only: the Consul builder exposed a
/// <c>BindConfiguration(string)</c> convenience, so every caller of the builder overload was treated as a
/// caller of the reflective configuration binder even when it never touched configuration. Removing that
/// one builder method makes the entry points genuinely trim-safe rather than annotated.
/// </para>
/// <para>
/// The safety arm asserts the annotations are gone. The liveness arm asserts the capability that method
/// provided is still reachable — the consumer binds the options themselves, which is the standard
/// <c>Microsoft.Extensions.Options</c> idiom and puts the trim warning at their own call site where the
/// trimmer can see it.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "LeaderElection")]
public sealed class ConsulRegistrationIsTrimSafeShould : UnitTestBase
{
	private static MethodInfo AddConsulLeaderElectionMethod() =>
		typeof(ConsulLeaderElectionExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(m => m.Name == "AddConsulLeaderElection"
				&& m.GetParameters()[1].ParameterType == typeof(Action<ILeaderElectionConsulBuilder>));

	private static MethodInfo UseConsulMethod() =>
		typeof(ConsulLeaderElectionBuilderExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(m => m.Name == "UseConsul");

	// --- Safety: the builder overloads carry no ahead-of-time hazard annotation ---

	[Fact]
	public void AddConsulLeaderElection_CarryNoAotAnnotation()
	{
		var method = AddConsulLeaderElectionMethod();

		method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldBeNull();
		method.GetCustomAttribute<RequiresDynamicCodeAttribute>().ShouldBeNull();
	}

	[Fact]
	public void UseConsul_CarryNoAotAnnotation()
	{
		var method = UseConsulMethod();

		method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldBeNull();
		method.GetCustomAttribute<RequiresDynamicCodeAttribute>().ShouldBeNull();
	}

	[Fact]
	public void ConsulBuilder_ExposeNoConfigurationBindingMethod()
	{
		// The builder is the seam that poisoned both entry points. A convenience re-added here would
		// re-annotate them, so the contract is locked on the interface, not only on the call sites.
		typeof(ILeaderElectionConsulBuilder)
			.GetMethod("BindConfiguration")
			.ShouldBeNull();
	}

	// --- Liveness: configuration binding still works, through the consumer-side idiom ---

	[Fact]
	public void BindOptionsFromConfiguration_WhenConsumerBindsThemselves()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Consul:LeaderElection:ConsulAddress"] = "http://consul.from-config:8500",
				["Consul:LeaderElection:Datacenter"] = "dc-from-config",
			})
			.Build();

		var services = new ServiceCollection();
		_ = services.AddSingleton<IConfiguration>(configuration);

		// The standard Microsoft.Extensions.Options route, stated by the consumer at their own call site.
		_ = services.AddOptions<ConsulLeaderElectionOptions>()
			.BindConfiguration("Consul:LeaderElection");

		_ = services.AddConsulLeaderElection(consul => consul.LockKey("service/leader"));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ConsulLeaderElectionOptions>>().Value;

		options.ConsulAddress.ShouldBe("http://consul.from-config:8500");
		options.Datacenter.ShouldBe("dc-from-config");
		options.KeyPrefix.ShouldBe("service/leader");
	}

	[Fact]
	public void LetTheFluentBuilderWinOverConfiguration_WhenBothSetTheSameValue()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Consul:LeaderElection:ConsulAddress"] = "http://consul.from-config:8500",
			})
			.Build();

		var services = new ServiceCollection();
		_ = services.AddSingleton<IConfiguration>(configuration);
		_ = services.AddOptions<ConsulLeaderElectionOptions>().BindConfiguration("Consul:LeaderElection");
		_ = services.AddConsulLeaderElection(consul => consul.Address("http://consul.explicit:8500"));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ConsulLeaderElectionOptions>>().Value;

		options.ConsulAddress.ShouldBe("http://consul.explicit:8500");
	}
}
