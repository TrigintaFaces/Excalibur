// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Extensions.Configuration;

using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.Postgres;

namespace Excalibur.Data.Tests.Postgres.Builders.LeaderElection;

/// <summary>
/// Locks the Postgres leader election registration entry point as trim- and native-AOT-safe, and locks the replacement
/// route for configuration binding.
/// </summary>
/// <remarks>
/// <para>
/// The entry point used to carry <see cref="RequiresUnreferencedCodeAttribute"/> and
/// <see cref="RequiresDynamicCodeAttribute"/> for one reason only: the builder exposed a
/// <c>BindConfiguration(string)</c> convenience, so every caller of the builder overload was treated as a
/// caller of the reflective configuration binder even when it never touched configuration. Removing that
/// one builder method makes the entry point genuinely trim-safe rather than annotated.
/// </para>
/// <para>
/// The safety arm asserts the annotations are gone. The liveness arm asserts the capability that method
/// provided is still reachable -- the consumer binds the options themselves, which is the standard
/// <c>Microsoft.Extensions.Options</c> idiom and puts the trim warning at their own call site where the
/// trimmer can see it.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresLeaderElectionRegistrationIsTrimSafeShould
{
	private sealed class TestLeaderElectionBuilder : ILeaderElectionBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	private static MethodInfo UsePostgresMethod() =>
		typeof(PostgresLeaderElectionBuilderExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(m => m.Name == "UsePostgres"
				&& m.GetParameters()[1].ParameterType == typeof(Action<IPostgresLeaderElectionBuilder>));

	// --- Safety: the builder overload carries no ahead-of-time hazard annotation ---

	[Fact]
	public void UsePostgres_CarryNoAotAnnotation()
	{
		var method = UsePostgresMethod();

		method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldBeNull();
		method.GetCustomAttribute<RequiresDynamicCodeAttribute>().ShouldBeNull();
	}

	[Fact]
	public void Builder_ExposeNoConfigurationBindingMethod()
	{
		// The builder is the seam that poisoned the entry point. A convenience re-added here would
		// re-annotate it, so the contract is locked on the interface, not only on the call site.
		typeof(IPostgresLeaderElectionBuilder)
			.GetMethod("BindConfiguration")
			.ShouldBeNull();
	}

	[Fact]
	public void NameTheOptionsTypeAConsumerCanActuallyName()
	{
		// This project has InternalsVisibleTo from the package under test, so the liveness arm above
		// compiles against PostgresLeaderElectionOptions whatever its visibility -- it is structurally blind to the
		// one property the shipped <remarks> depends on. A consumer has no such grant, so an internal
		// options type would make that guidance uncompilable for them while every test still passed.
		typeof(PostgresLeaderElectionOptions).IsPublic.ShouldBeTrue();
	}

	// --- Liveness: configuration binding still works, through the consumer-side idiom ---

	[Fact]
	public void BindOptionsFromConfiguration_WhenConsumerBindsThemselves()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["LeaderElection:Postgres:ConnectionString"] = "Host=from-config;Database=Db;Username=u;Password=p",
			})
			.Build();

		var builder = new TestLeaderElectionBuilder();
		_ = builder.Services.AddSingleton<IConfiguration>(configuration);

		_ = builder.UsePostgres(pg => pg.LockKey(42));

		// The standard Microsoft.Extensions.Options route, stated by the consumer at their own call site.
		_ = builder.Services.AddOptions<PostgresLeaderElectionOptions>()
			.BindConfiguration("LeaderElection:Postgres");

		using var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<PostgresLeaderElectionOptions>>().Value;

		options.ConnectionString.ShouldBe("Host=from-config;Database=Db;Username=u;Password=p");
		options.LockKey.ShouldBe(42);
	}
}
