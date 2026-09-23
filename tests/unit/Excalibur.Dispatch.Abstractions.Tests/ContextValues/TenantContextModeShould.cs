// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Abstractions.Tests.ContextValues;

/// <summary>
/// Locks how contributed tenant-context modes resolve: highest precedence wins, whatever the order, and an
/// ambiguous choice is refused rather than made.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantContextModeShould
{
	/// <summary>The highest precedence resolves, in both registration orders.</summary>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void ResolveTheHighestPrecedence_InEitherOrder(bool lowFirst)
	{
		var services = new ServiceCollection();

		if (lowFirst)
		{
			_ = services.AddTenantContextMode<LowMode>().AddTenantContextMode<HighMode>();
		}
		else
		{
			_ = services.AddTenantContextMode<HighMode>().AddTenantContextMode<LowMode>();
		}

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<ITenantContext>().TenantId.ShouldBe("high");
	}

	/// <summary>
	/// Two different modes of equal highest precedence are refused on first resolution, naming both.
	/// </summary>
	[Fact]
	public void RefuseTwoModesOfEqualHighestPrecedence_OnResolution()
	{
		var services = new ServiceCollection();
		_ = services.AddTenantContextMode<HighMode>().AddTenantContextMode<OtherHighMode>();

		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<ITenantContext>());
		ex.Message.ShouldContain(nameof(HighMode));
		ex.Message.ShouldContain(nameof(OtherHighMode));
	}

	/// <summary>The same ambiguity is reported when the host starts, before anything resolves the tenant.</summary>
	[Fact]
	public void RefuseTwoModesOfEqualHighestPrecedence_AtStartup()
	{
		var services = new ServiceCollection();
		_ = services.AddTenantContextMode<HighMode>().AddTenantContextMode<OtherHighMode>();

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
	}

	/// <summary>LIVENESS: modes of different precedence start cleanly.</summary>
	[Fact]
	public void StartCleanly_WhenPrecedencesDiffer()
	{
		var services = new ServiceCollection();
		_ = services.AddTenantContextMode<HighMode>().AddTenantContextMode<LowMode>();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
	}

	/// <summary>Contributing the same mode twice contributes it once.</summary>
	[Fact]
	public void ContributeAModeOnce_WhenItIsRegisteredTwice()
	{
		var services = new ServiceCollection();
		_ = services.AddTenantContextMode<HighMode>().AddTenantContextMode<HighMode>();

		using var provider = services.BuildServiceProvider();

		// The single-tenant default plus one HighMode, not two: a duplicate would also tie with itself.
		provider.GetServices<ITenantContextMode>().Count().ShouldBe(2);
		provider.GetRequiredService<ITenantContext>().TenantId.ShouldBe("high");
	}

	/// <summary>A context registered directly takes the place of every mode.</summary>
	[Fact]
	public void LetADirectlyRegisteredContextTakePrecedenceOverEveryMode()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton<ITenantContext>(new FixedContext("direct"));
		_ = services.AddTenantContextMode<HighMode>();

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<ITenantContext>().TenantId.ShouldBe("direct");
	}

	private sealed class LowMode : ITenantContextMode
	{
		public int Precedence => 10;

		public ITenantContext Create(IServiceProvider services) => new FixedContext("low");
	}

	private sealed class HighMode : ITenantContextMode
	{
		public int Precedence => 20;

		public ITenantContext Create(IServiceProvider services) => new FixedContext("high");
	}

	private sealed class OtherHighMode : ITenantContextMode
	{
		public int Precedence => 20;

		public ITenantContext Create(IServiceProvider services) => new FixedContext("other-high");
	}

	private sealed class FixedContext(string tenantId) : ITenantContext
	{
		public string? TenantId => tenantId;

		public bool HasTenant => true;
	}
}
