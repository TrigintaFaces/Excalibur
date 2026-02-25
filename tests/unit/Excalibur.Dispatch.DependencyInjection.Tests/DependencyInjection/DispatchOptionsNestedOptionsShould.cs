// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Unit tests for the 4 nested options classes added to <see cref="DispatchOptions"/>
/// in Sprint 501 (bd-79if7).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
public sealed class DispatchOptionsNestedOptionsShould
{
	#region SecurityOptions Defaults (AC-1)

	[Fact]
	public void SecurityOptions_DefaultEnableEncryption_IsFalse()
	{
		var options = new SecurityOptions();
		options.EnableEncryption.ShouldBeFalse();
	}

	[Fact]
	public void SecurityOptions_DefaultEnableSigning_IsFalse()
	{
		var options = new SecurityOptions();
		options.EnableSigning.ShouldBeFalse();
	}

	[Fact]
	public void SecurityOptions_DefaultEnableRateLimiting_IsFalse()
	{
		var options = new SecurityOptions();
		options.EnableRateLimiting.ShouldBeFalse();
	}

	[Fact]
	public void SecurityOptions_DefaultEnableValidation_IsTrue()
	{
		var options = new SecurityOptions();
		options.EnableValidation.ShouldBeTrue();
	}

	#endregion

	#region ObservabilityOptions Defaults (AC-2)

	[Fact]
	public void ObservabilityOptions_DefaultEnabled_IsTrue()
	{
		var options = new ObservabilityOptions();
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void ObservabilityOptions_DefaultEnableTracing_IsTrue()
	{
		var options = new ObservabilityOptions();
		options.EnableTracing.ShouldBeTrue();
	}

	[Fact]
	public void ObservabilityOptions_DefaultEnableMetrics_IsTrue()
	{
		var options = new ObservabilityOptions();
		options.EnableMetrics.ShouldBeTrue();
	}

	[Fact]
	public void ObservabilityOptions_DefaultEnableContextFlow_IsTrue()
	{
		var options = new ObservabilityOptions();
		options.EnableContextFlow.ShouldBeTrue();
	}

	#endregion

	#region ResilienceOptions Defaults (AC-3)

	[Fact]
	public void ResilienceOptions_DefaultRetryCount_IsThree()
	{
		var options = new ResilienceOptions();
		options.DefaultRetryCount.ShouldBe(3);
	}

	[Fact]
	public void ResilienceOptions_DefaultEnableCircuitBreaker_IsFalse()
	{
		var options = new ResilienceOptions();
		options.EnableCircuitBreaker.ShouldBeFalse();
	}

	[Fact]
	public void ResilienceOptions_DefaultEnableTimeout_IsFalse()
	{
		var options = new ResilienceOptions();
		options.EnableTimeout.ShouldBeFalse();
	}

	[Fact]
	public void ResilienceOptions_DefaultEnableBulkhead_IsFalse()
	{
		var options = new ResilienceOptions();
		options.EnableBulkhead.ShouldBeFalse();
	}

	#endregion

	#region CachingOptions Defaults (AC-4)

	[Fact]
	public void CachingOptions_DefaultEnabled_IsFalse()
	{
		var options = new CachingOptions();
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void CachingOptions_DefaultExpiration_IsFiveMinutes()
	{
		var options = new CachingOptions();
		options.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region DispatchOptions Nested Properties (AC-5, AC-6)

	[Fact]
	public void DispatchOptions_SecurityProperty_IsInitialized()
	{
		var options = new DispatchOptions();
		_ = options.CrossCutting.Security.ShouldNotBeNull();
		_ = options.CrossCutting.Security.ShouldBeOfType<SecurityOptions>();
	}

	[Fact]
	public void DispatchOptions_ObservabilityProperty_IsInitialized()
	{
		var options = new DispatchOptions();
		_ = options.CrossCutting.Observability.ShouldNotBeNull();
		_ = options.CrossCutting.Observability.ShouldBeOfType<ObservabilityOptions>();
	}

	[Fact]
	public void DispatchOptions_ResilienceProperty_IsInitialized()
	{
		var options = new DispatchOptions();
		_ = options.CrossCutting.Resilience.ShouldNotBeNull();
		_ = options.CrossCutting.Resilience.ShouldBeOfType<ResilienceOptions>();
	}

	[Fact]
	public void DispatchOptions_CachingProperty_IsInitialized()
	{
		var options = new DispatchOptions();
		_ = options.CrossCutting.Caching.ShouldNotBeNull();
		_ = options.CrossCutting.Caching.ShouldBeOfType<CachingOptions>();
	}

	[Fact]
	public void DispatchOptions_ExistingNestedProperties_Unchanged()
	{
		// Verify existing nested properties (Inbox, Outbox, Consumer, Performance)
		// still work after the Sprint 501 additions
		var options = new DispatchOptions();
		_ = options.Inbox.ShouldNotBeNull();
		_ = options.Outbox.ShouldNotBeNull();
		_ = options.Consumer.ShouldNotBeNull();
		_ = options.CrossCutting.Performance.ShouldNotBeNull();
	}

	#endregion

	#region Options Configuration (Setter Tests)

	[Fact]
	public void SecurityOptions_CanSetProperties()
	{
		var options = new SecurityOptions
		{
			EnableEncryption = true,
			EnableSigning = true,
			EnableRateLimiting = true,
			EnableValidation = false,
		};

		options.EnableEncryption.ShouldBeTrue();
		options.EnableSigning.ShouldBeTrue();
		options.EnableRateLimiting.ShouldBeTrue();
		options.EnableValidation.ShouldBeFalse();
	}

	[Fact]
	public void ObservabilityOptions_CanSetProperties()
	{
		var options = new ObservabilityOptions
		{
			Enabled = false,
			EnableTracing = false,
			EnableMetrics = false,
			EnableContextFlow = false,
		};

		options.Enabled.ShouldBeFalse();
		options.EnableTracing.ShouldBeFalse();
		options.EnableMetrics.ShouldBeFalse();
		options.EnableContextFlow.ShouldBeFalse();
	}

	[Fact]
	public void ResilienceOptions_CanSetProperties()
	{
		var options = new ResilienceOptions
		{
			DefaultRetryCount = 5,
			EnableCircuitBreaker = true,
			EnableTimeout = true,
			EnableBulkhead = true,
		};

		options.DefaultRetryCount.ShouldBe(5);
		options.EnableCircuitBreaker.ShouldBeTrue();
		options.EnableTimeout.ShouldBeTrue();
		options.EnableBulkhead.ShouldBeTrue();
	}

	[Fact]
	public void CachingOptions_CanSetProperties()
	{
		var options = new CachingOptions
		{
			Enabled = true,
			DefaultExpiration = TimeSpan.FromMinutes(10),
		};

		options.Enabled.ShouldBeTrue();
		options.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void DispatchOptions_CanReplaceNestedOptions()
	{
		var options = new DispatchOptions();
		var customSecurity = new SecurityOptions { EnableEncryption = true };

		options.CrossCutting.Security = customSecurity;

		options.CrossCutting.Security.ShouldBeSameAs(customSecurity);
		options.CrossCutting.Security.EnableEncryption.ShouldBeTrue();
	}

	#endregion
}
