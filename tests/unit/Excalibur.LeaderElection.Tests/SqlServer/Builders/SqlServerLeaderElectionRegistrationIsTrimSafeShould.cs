// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Extensions.Configuration;

using Tests.Shared.Categories;

namespace Excalibur.LeaderElection.Tests.SqlServer.Builders;

/// <summary>
/// Locks the SQL Server leader election registration entry point as trim- and native-AOT-safe, and locks the replacement
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
[Trait(TraitNames.Component, "LeaderElection")]
public sealed class SqlServerLeaderElectionRegistrationIsTrimSafeShould
{
	private sealed class TestLeaderElectionBuilder : ILeaderElectionBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	private static MethodInfo UseSqlServerMethod() =>
		typeof(SqlServerLeaderElectionBuilderExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(m => m.Name == "UseSqlServer"
				&& m.GetParameters()[1].ParameterType == typeof(Action<ISqlServerLeaderElectionBuilder>));

	// --- Safety: the builder overload carries no ahead-of-time hazard annotation ---

	[Fact]
	public void UseSqlServer_CarryNoAotAnnotation()
	{
		var method = UseSqlServerMethod();

		method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldBeNull();
		method.GetCustomAttribute<RequiresDynamicCodeAttribute>().ShouldBeNull();
	}

	[Fact]
	public void Builder_ExposeNoConfigurationBindingMethod()
	{
		// The builder is the seam that poisoned the entry point. A convenience re-added here would
		// re-annotate it, so the contract is locked on the interface, not only on the call site.
		typeof(ISqlServerLeaderElectionBuilder)
			.GetMethod("BindConfiguration")
			.ShouldBeNull();
	}

	[Fact]
	public void NameTheOptionsTypeAConsumerCanActuallyName()
	{
		// This project has InternalsVisibleTo from the package under test, so the liveness arm above
		// compiles against SqlServerLeaderElectionOptions whatever its visibility -- it is structurally blind to the
		// one property the shipped <remarks> depends on. A consumer has no such grant, so an internal
		// options type would make that guidance uncompilable for them while every test still passed.
		typeof(SqlServerLeaderElectionOptions).IsPublic.ShouldBeTrue();
	}

	// --- Liveness: configuration binding still works, through the consumer-side idiom ---

	[Fact]
	public void BindOptionsFromConfiguration_WhenConsumerBindsThemselves()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["LeaderElection:SqlServer:ConnectionString"] = "Server=from-config;Database=Db;Integrated Security=true;",
			})
			.Build();

		var builder = new TestLeaderElectionBuilder();
		_ = builder.Services.AddSingleton<IConfiguration>(configuration);

		_ = builder.UseSqlServer(sql => sql.LockResource("MyApp.Leader"));

		// The standard Microsoft.Extensions.Options route, stated by the consumer at their own call site.
		_ = builder.Services.AddOptions<SqlServerLeaderElectionOptions>()
			.BindConfiguration("LeaderElection:SqlServer");

		using var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerLeaderElectionOptions>>().Value;

		options.ConnectionString.ShouldBe("Server=from-config;Database=Db;Integrated Security=true;");
		options.LockResource.ShouldBe("MyApp.Leader");
	}
}
