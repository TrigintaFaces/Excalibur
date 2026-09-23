// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Hosting.Tests.Builders;

/// <summary>
/// Locks that a default tenant the consumer configured is the default tenant they get, whatever order
/// it is registered in relative to <c>AddExcalibur</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every arm asserts the resolved VALUE. The pre-existing smoke arm asserts only that
/// <c>UseTenant</c> contributes a configuration, and it passed throughout the period in which that
/// configuration was being overwritten: counting configurations cannot see which one wins.
/// </para>
/// <para>
/// Options configuration actions run in registration order and the last write wins, so the defect
/// depended on order. Each order therefore gets its own arm. An arm for only one order would pass a
/// fix that repaired that order and left the other one losing the consumer's value.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Tenancy")]
public sealed class ConfiguredDefaultTenantSurvivesShould : UnitTestBase
{
	private const string ConsumerTenant = "consumer-tenant";

	/// <summary>
	/// SAFETY: <c>UseTenant</c> inside the <c>AddExcalibur</c> callback is not overwritten by the
	/// framework default that <c>AddExcalibur</c> registers after the callback returns.
	/// </summary>
	[Fact]
	public void KeepUseTenantInsideTheAddExcaliburCallback()
	{
		var services = new ServiceCollection();

		_ = services.AddExcalibur(b => b.UseTenant(ConsumerTenant));

		ResolvedDefaultTenant(services).ShouldBe(ConsumerTenant);
	}

	/// <summary>
	/// SAFETY: a default tenant configured BEFORE <c>AddExcalibur</c> is not overwritten by it.
	/// </summary>
	[Fact]
	public void KeepATenantConfiguredBeforeAddExcalibur()
	{
		var services = new ServiceCollection();

		_ = services.Configure<TenantContextOptions>(o => o.DefaultTenantId = ConsumerTenant);
		_ = services.AddExcalibur(static _ => { });

		ResolvedDefaultTenant(services).ShouldBe(ConsumerTenant);
	}

	/// <summary>
	/// SAFETY: a default tenant configured AFTER <c>AddExcalibur</c> is kept.
	/// </summary>
	/// <remarks>
	/// This order was already correct - the consumer's write simply came last - and is locked so that a
	/// fix to the other two orders cannot trade this one away.
	/// </remarks>
	[Fact]
	public void KeepATenantConfiguredAfterAddExcalibur()
	{
		var services = new ServiceCollection();

		_ = services.AddExcalibur(static _ => { });
		_ = services.Configure<TenantContextOptions>(o => o.DefaultTenantId = ConsumerTenant);

		ResolvedDefaultTenant(services).ShouldBe(ConsumerTenant);
	}

	/// <summary>
	/// LIVENESS: with nothing configured, the framework default IS seated.
	/// </summary>
	/// <remarks>
	/// Without this arm every safety arm above is satisfied by a fix that simply stops seating a default.
	/// </remarks>
	[Fact]
	public void SeatTheFrameworkDefaultWhenTheConsumerConfiguredNone()
	{
		var services = new ServiceCollection();

		_ = services.AddExcalibur(static _ => { });

		ResolvedDefaultTenant(services).ShouldBe(TenantDefaults.DefaultTenantId);
	}

	private static string? ResolvedDefaultTenant(IServiceCollection services)
	{
		using var provider = services.BuildServiceProvider();
		return provider.GetRequiredService<IOptions<TenantContextOptions>>().Value.DefaultTenantId;
	}
}
