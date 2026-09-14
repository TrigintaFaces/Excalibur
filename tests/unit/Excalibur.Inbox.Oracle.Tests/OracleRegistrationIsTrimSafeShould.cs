// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Inbox.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// Locks the Oracle inbox registration entry point as trim- and native-AOT-safe, and locks the replacement
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
[Trait("Category", "Unit")]
[Trait("Component", "Persistence")]
[Trait("Database", "Oracle")]
public sealed class OracleRegistrationIsTrimSafeShould
{
	private sealed class TestInboxBuilder : IInboxBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	private static MethodInfo UseOracleMethod() =>
		typeof(InboxBuilderOracleExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(m => m.Name == "UseOracle"
				&& m.GetParameters()[1].ParameterType == typeof(Action<IOracleInboxBuilder>));

	// --- Safety: the builder overload carries no ahead-of-time hazard annotation ---

	[Fact]
	public void UseOracle_CarryNoAotAnnotation()
	{
		var method = UseOracleMethod();

		method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldBeNull();
		method.GetCustomAttribute<RequiresDynamicCodeAttribute>().ShouldBeNull();
	}

	[Fact]
	public void Builder_ExposeNoConfigurationBindingMethod()
	{
		// The builder is the seam that poisoned the entry point. A convenience re-added here would
		// re-annotate it, so the contract is locked on the interface, not only on the call site.
		typeof(IOracleInboxBuilder)
			.GetMethod("BindConfiguration")
			.ShouldBeNull();
	}

	[Fact]
	public void NameTheOptionsTypeAConsumerCanActuallyName()
	{
		// This project has InternalsVisibleTo from the package under test, so the liveness arm above
		// compiles against OracleInboxOptions whatever its visibility -- it is structurally blind to the
		// one property the shipped <remarks> depends on. A consumer has no such grant, so an internal
		// options type would make that guidance uncompilable for them while every test still passed.
		typeof(OracleInboxOptions).IsPublic.ShouldBeTrue();
	}

	// --- Liveness: configuration binding still works, through the consumer-side idiom ---

	[Fact]
	public void BindOptionsFromConfiguration_WhenConsumerBindsThemselves()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Inbox:Oracle:ConnectionString"] = "User Id=u;Password=p;Data Source=//from-config:1521/FREEPDB1",
				["Inbox:Oracle:SchemaName"] = "APPCFG",
			})
			.Build();

		var builder = new TestInboxBuilder();
		_ = builder.Services.AddSingleton<IConfiguration>(configuration);

		_ = builder.UseOracle(oracle => oracle.TableName("INBOX_FROM_BUILDER"));

		// The standard Microsoft.Extensions.Options route, stated by the consumer at their own call site.
		_ = builder.Services.AddOptions<OracleInboxOptions>()
			.BindConfiguration("Inbox:Oracle");

		using var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<OracleInboxOptions>>().Value;

		options.ConnectionString.ShouldBe("User Id=u;Password=p;Data Source=//from-config:1521/FREEPDB1");
		options.SchemaName.ShouldBe("APPCFG");
		options.TableName.ShouldBe("INBOX_FROM_BUILDER");
	}
}
