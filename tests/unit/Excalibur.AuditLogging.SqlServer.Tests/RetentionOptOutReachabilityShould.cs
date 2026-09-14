// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging.Retention;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.SqlServer.Tests;

/// <summary>
/// The retention opt-out a consumer sets must reach the service that performs the deletion.
/// </summary>
/// <remarks>
/// <para>
/// The switch a SQL Server consumer configures lives on <c>SqlServerAuditRetentionOptions</c>; the service
/// that deletes reads <see cref="AuditRetentionOptions"/>. The two types are unrelated — both sealed, no
/// inheritance — so the provider-facing switch is only connected to the enforcing service by an explicit
/// projection. Without it, a consumer sets the flag <see langword="false"/>, the service reads its own
/// default of <see langword="true"/>, and their audit data is deleted against an explicit instruction.
/// </para>
/// <para>
/// <b>Why this asserts through the registration path and not the options type.</b> The obvious arm —
/// construct <c>AuditRetentionOptions</c> with the flag false and assert the service does not purge —
/// <b>passes even when the projection is entirely absent</b>, because it hand-sets the value the gate reads
/// and bypasses the step that is actually broken. It proves the gate, which was never in doubt. This arm
/// instead resolves the options the way the framework does, from a container built by the consumer-facing
/// registration, so it can only pass if the consumer's setting genuinely arrives.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RetentionOptOutReachabilityShould
{
	[Fact]
	public void Carry_a_consumers_opt_out_through_to_the_enforcing_service()
	{
		// SAFETY: the consumer says "do not enforce retention" on the surface they configure.
		using var provider = BuildProvider(enableRetentionEnforcement: false);

		var enforcing = provider.GetRequiredService<IOptions<AuditRetentionOptions>>().Value;

		enforcing.EnableRetentionEnforcement.ShouldBeFalse(
			"the opt-out a consumer sets on the SQL Server options must reach the type the deleting service "
			+ "reads — otherwise the switch is connected to nothing and their audit data is destroyed against "
			+ "an explicit instruction, with no error on either side.");
	}

	[Fact]
	public void Leave_retention_enabled_when_the_consumer_does_not_opt_out()
	{
		// LIVENESS. Without this arm the safety half above is satisfied by a projection hard-wired to false,
		// which would disable retention for every consumer — silently retaining data past its policy, the
		// exact failure the deletion path was repaired to prevent.
		using var provider = BuildProvider(enableRetentionEnforcement: true);

		provider.GetRequiredService<IOptions<AuditRetentionOptions>>().Value
			.EnableRetentionEnforcement.ShouldBeTrue(
				"a consumer who does not opt out must still get retention enforcement — an opt-out that is "
				+ "always on is not an opt-out, it is an outage.");
	}

	[Fact]
	public void Default_to_enabled_when_the_consumer_expresses_no_preference()
	{
		// The default is deliberately TRUE: retention is a control, and a control that ships off by default
		// is a promise nobody keeps. A consumer who configures nothing gets enforcement.
		using var provider = BuildProvider(configureRetention: null);

		provider.GetRequiredService<IOptions<AuditRetentionOptions>>().Value
			.EnableRetentionEnforcement.ShouldBeTrue(
				"absent an explicit opt-out, retention must remain enabled.");
	}

	[Fact]
	public void Carry_a_consumers_retention_window_through_to_the_enforcing_service()
	{
		// SAFETY: the window is the value the sweep deletes behind. Left unprojected, a consumer who asks for
		// ninety days keeps every audit event for the core default of seven years — no error, no warning, and
		// the only symptom is data that should have been destroyed still being there years later.
		using var provider = BuildProvider(o => o.Retention.RetentionPeriod = TimeSpan.FromDays(90));

		provider.GetRequiredService<IOptions<AuditRetentionOptions>>().Value
			.RetentionPeriod.ShouldBe(TimeSpan.FromDays(90),
				"the retention window a consumer sets on the SQL Server options block must reach the type the "
				+ "sweep reads — that block sits beside the connection string, which is where a retention "
				+ "window looks like it belongs.");
	}

	[Fact]
	public void Carry_a_consumers_cleanup_interval_through_to_the_enforcing_service()
	{
		// SAFETY: the interval is what the retention background service waits between sweeps.
		using var provider = BuildProvider(o => o.Retention.CleanupInterval = TimeSpan.FromHours(6));

		provider.GetRequiredService<IOptions<AuditRetentionOptions>>().Value
			.CleanupInterval.ShouldBe(TimeSpan.FromHours(6),
				"the sweep interval a consumer sets on the SQL Server options block must reach the background "
				+ "service that schedules the sweep.");
	}

	[Fact]
	public void Leave_the_core_defaults_in_place_when_the_consumer_configures_no_retention()
	{
		// LIVENESS for both arms above. Without it a projection hard-wired to a constant satisfies them, and
		// every consumer silently inherits that constant instead of the documented default.
		using var provider = BuildProvider(configureRetention: null);

		var enforcing = provider.GetRequiredService<IOptions<AuditRetentionOptions>>().Value;

		enforcing.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7 * 365),
			"a consumer who configures no window must get the shipped seven-year default, not whatever "
			+ "constant a projection happens to carry.");
		enforcing.CleanupInterval.ShouldBe(TimeSpan.FromDays(1),
			"a consumer who configures no interval must get the shipped daily default.");
	}

	[Fact]
	public void Not_overwrite_a_window_the_host_already_set_on_the_core_options()
	{
		// The projection must not turn registration ORDER into a retention policy. A host that sets the
		// window on AuditRetentionOptions and then registers the store has expressed one preference; the
		// store's own untouched default must not land on top of it. This arm fails if the projection is
		// unconditional, and the direction it fails in — seven years replacing ninety days — over-retains.
		var services = new ServiceCollection();
		services.AddLogging();

		_ = services.Configure<AuditRetentionOptions>(o => o.RetentionPeriod = TimeSpan.FromDays(90));

		_ = services.AddSqlServerAuditStore(o =>
		{
			o.ConnectionString = "Server=(local);Database=Test;Integrated Security=true;";
			o.SchemaName = "audit";
			o.TableName = "AuditEvents";
		});

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IOptions<AuditRetentionOptions>>().Value
			.RetentionPeriod.ShouldBe(TimeSpan.FromDays(90),
				"registering the SQL Server store must not silently replace a retention window the host had "
				+ "already chosen — the provider block carries a default, and a default is not a choice.");
	}

	private static ServiceProvider BuildProvider(bool enableRetentionEnforcement) =>
		BuildProvider(o => o.Retention.EnableRetentionEnforcement = enableRetentionEnforcement);

	/// <summary>
	/// Builds the container through the consumer-facing registration — the whole point of these arms. The
	/// projection under test lives inside that call, so resolving the options from here exercises it.
	/// </summary>
	private static ServiceProvider BuildProvider(Action<SqlServerAuditOptions>? configureRetention)
	{
		var services = new ServiceCollection();
		services.AddLogging();

		_ = services.AddSqlServerAuditStore(o =>
		{
			o.ConnectionString = "Server=(local);Database=Test;Integrated Security=true;";
			o.SchemaName = "audit";
			o.TableName = "AuditEvents";
			configureRetention?.Invoke(o);
		});

		return services.BuildServiceProvider();
	}
}
