// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using Excalibur.Caching.AdaptiveTtl;

namespace Excalibur.Tests.Caching.AdaptiveTtl;

/// <summary>
/// Verifies the ISP split of RuleBasedTtlOptions into sub-options
/// (HitRate, Frequency, Load, TimeOfDay, Content) and confirms shim removal.
/// Sprint 564 S564.53: RuleBasedTtlOptions + AdaptiveTtlOptions ISP split verification.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class RuleBasedTtlOptionsIspSplitShould
{
	#region Root Property Count (ISP Gate)

	[Fact]
	public void RuleBasedTtlOptions_RootHaveAtMost10Properties()
	{
		// DeclaredOnly to avoid inherited AdaptiveTtlOptions properties
		var properties = typeof(RuleBasedTtlOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"RuleBasedTtlOptions has {properties.Length} declared properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void AdaptiveTtlOptions_RootHaveAtMost10Properties()
	{
		var properties = typeof(AdaptiveTtlOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"AdaptiveTtlOptions has {properties.Length} declared properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void HitRateSubOptions_HaveAtMost10Properties()
	{
		var properties = typeof(RuleBasedHitRateOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"RuleBasedHitRateOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void FrequencySubOptions_HaveAtMost10Properties()
	{
		var properties = typeof(RuleBasedFrequencyOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"RuleBasedFrequencyOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void LoadSubOptions_HaveAtMost10Properties()
	{
		var properties = typeof(RuleBasedLoadOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"RuleBasedLoadOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void TimeOfDaySubOptions_HaveAtMost10Properties()
	{
		var properties = typeof(RuleBasedTimeOfDayOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"RuleBasedTimeOfDayOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void ContentSubOptions_HaveAtMost10Properties()
	{
		var properties = typeof(RuleBasedContentOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"RuleBasedContentOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void WeightSubOptions_HaveAtMost10Properties()
	{
		var properties = typeof(AdaptiveTtlWeightOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"AdaptiveTtlWeightOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void ThresholdSubOptions_HaveAtMost10Properties()
	{
		var properties = typeof(AdaptiveTtlThresholdOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"AdaptiveTtlThresholdOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	#endregion

	#region Sub-Options Initialized

	[Fact]
	public void RuleBasedTtlOptions_HaveNonNullHitRateSubOptions()
	{
		var options = new RuleBasedTtlOptions();
		options.HitRate.ShouldNotBeNull();
	}

	[Fact]
	public void RuleBasedTtlOptions_HaveNonNullFrequencySubOptions()
	{
		var options = new RuleBasedTtlOptions();
		options.Frequency.ShouldNotBeNull();
	}

	[Fact]
	public void RuleBasedTtlOptions_HaveNonNullLoadSubOptions()
	{
		var options = new RuleBasedTtlOptions();
		options.Load.ShouldNotBeNull();
	}

	[Fact]
	public void RuleBasedTtlOptions_HaveNonNullTimeOfDaySubOptions()
	{
		var options = new RuleBasedTtlOptions();
		options.TimeOfDay.ShouldNotBeNull();
	}

	[Fact]
	public void RuleBasedTtlOptions_HaveNonNullContentSubOptions()
	{
		var options = new RuleBasedTtlOptions();
		options.Content.ShouldNotBeNull();
	}

	[Fact]
	public void AdaptiveTtlOptions_HaveNonNullWeightsSubOptions()
	{
		var options = new AdaptiveTtlOptions();
		options.Weights.ShouldNotBeNull();
	}

	[Fact]
	public void AdaptiveTtlOptions_HaveNonNullThresholdsSubOptions()
	{
		var options = new AdaptiveTtlOptions();
		options.Thresholds.ShouldNotBeNull();
	}

	#endregion

	#region HitRate Default Values

	[Fact]
	public void HitRate_HaveDefaultHighHitRateThreshold()
	{
		var hr = new RuleBasedHitRateOptions();
		hr.HighHitRateThreshold.ShouldBe(0.9);
	}

	[Fact]
	public void HitRate_HaveDefaultLowHitRateThreshold()
	{
		var hr = new RuleBasedHitRateOptions();
		hr.LowHitRateThreshold.ShouldBe(0.5);
	}

	[Fact]
	public void HitRate_HaveDefaultHighHitRateMultiplier()
	{
		var hr = new RuleBasedHitRateOptions();
		hr.HighHitRateMultiplier.ShouldBe(1.5);
	}

	[Fact]
	public void HitRate_HaveDefaultLowHitRateMultiplier()
	{
		var hr = new RuleBasedHitRateOptions();
		hr.LowHitRateMultiplier.ShouldBe(0.7);
	}

	#endregion

	#region Frequency Default Values

	[Fact]
	public void Frequency_HaveDefaultHighFrequencyThreshold()
	{
		var freq = new RuleBasedFrequencyOptions();
		freq.HighFrequencyThreshold.ShouldBe(100);
	}

	[Fact]
	public void Frequency_HaveDefaultLowFrequencyThreshold()
	{
		var freq = new RuleBasedFrequencyOptions();
		freq.LowFrequencyThreshold.ShouldBe(1);
	}

	[Fact]
	public void Frequency_HaveDefaultHighFrequencyMultiplier()
	{
		var freq = new RuleBasedFrequencyOptions();
		freq.HighFrequencyMultiplier.ShouldBe(1.4);
	}

	[Fact]
	public void Frequency_HaveDefaultLowFrequencyMultiplier()
	{
		var freq = new RuleBasedFrequencyOptions();
		freq.LowFrequencyMultiplier.ShouldBe(0.8);
	}

	#endregion

	#region Load Default Values

	[Fact]
	public void Load_HaveDefaultExpensiveMissThreshold()
	{
		var load = new RuleBasedLoadOptions();
		load.ExpensiveMissThreshold.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void Load_HaveDefaultExpensiveMissMultiplier()
	{
		var load = new RuleBasedLoadOptions();
		load.ExpensiveMissMultiplier.ShouldBe(1.3);
	}

	[Fact]
	public void Load_HaveDefaultHighLoadMultiplier()
	{
		var load = new RuleBasedLoadOptions();
		load.HighLoadMultiplier.ShouldBe(0.7);
	}

	[Fact]
	public void Load_HaveDefaultLowLoadMultiplier()
	{
		var load = new RuleBasedLoadOptions();
		load.LowLoadMultiplier.ShouldBe(1.2);
	}

	#endregion

	#region TimeOfDay Default Values

	[Fact]
	public void TimeOfDay_HaveDefaultPeakHoursStart()
	{
		var tod = new RuleBasedTimeOfDayOptions();
		tod.PeakHoursStart.ShouldBe(9);
	}

	[Fact]
	public void TimeOfDay_HaveDefaultPeakHoursEnd()
	{
		var tod = new RuleBasedTimeOfDayOptions();
		tod.PeakHoursEnd.ShouldBe(17);
	}

	[Fact]
	public void TimeOfDay_HaveDefaultOffHoursStart()
	{
		var tod = new RuleBasedTimeOfDayOptions();
		tod.OffHoursStart.ShouldBe(22);
	}

	[Fact]
	public void TimeOfDay_HaveDefaultOffHoursEnd()
	{
		var tod = new RuleBasedTimeOfDayOptions();
		tod.OffHoursEnd.ShouldBe(6);
	}

	[Fact]
	public void TimeOfDay_HaveDefaultPeakHoursMultiplier()
	{
		var tod = new RuleBasedTimeOfDayOptions();
		tod.PeakHoursMultiplier.ShouldBe(1.2);
	}

	[Fact]
	public void TimeOfDay_HaveDefaultOffHoursMultiplier()
	{
		var tod = new RuleBasedTimeOfDayOptions();
		tod.OffHoursMultiplier.ShouldBe(0.8);
	}

	#endregion

	#region Content Default Values

	[Fact]
	public void Content_HaveDefaultLargeContentThreshold()
	{
		var content = new RuleBasedContentOptions();
		content.LargeContentThreshold.ShouldBe(1024 * 1024); // 1MB
	}

	[Fact]
	public void Content_HaveDefaultLargeContentMultiplier()
	{
		var content = new RuleBasedContentOptions();
		content.LargeContentMultiplier.ShouldBe(0.9);
	}

	#endregion

	#region Nested Initializer Tests

	[Fact]
	public void SupportNestedInitializerForHitRate()
	{
		var options = new RuleBasedTtlOptions
		{
			HitRate = new RuleBasedHitRateOptions
			{
				HighHitRateThreshold = 0.95,
				LowHitRateThreshold = 0.3,
				HighHitRateMultiplier = 2.0,
				LowHitRateMultiplier = 0.5,
			},
		};

		options.HitRate.HighHitRateThreshold.ShouldBe(0.95);
		options.HitRate.LowHitRateThreshold.ShouldBe(0.3);
		options.HitRate.HighHitRateMultiplier.ShouldBe(2.0);
		options.HitRate.LowHitRateMultiplier.ShouldBe(0.5);
	}

	[Fact]
	public void SupportNestedInitializerForFrequency()
	{
		var options = new RuleBasedTtlOptions
		{
			Frequency = new RuleBasedFrequencyOptions
			{
				HighFrequencyThreshold = 200,
				LowFrequencyThreshold = 5,
			},
		};

		options.Frequency.HighFrequencyThreshold.ShouldBe(200);
		options.Frequency.LowFrequencyThreshold.ShouldBe(5);
	}

	[Fact]
	public void SupportNestedInitializerForTimeOfDay()
	{
		var options = new RuleBasedTtlOptions
		{
			TimeOfDay = new RuleBasedTimeOfDayOptions
			{
				PeakHoursStart = 8,
				PeakHoursEnd = 18,
				OffHoursStart = 23,
				OffHoursEnd = 5,
				PeakHoursMultiplier = 1.5,
				OffHoursMultiplier = 0.6,
			},
		};

		options.TimeOfDay.PeakHoursStart.ShouldBe(8);
		options.TimeOfDay.PeakHoursEnd.ShouldBe(18);
		options.TimeOfDay.PeakHoursMultiplier.ShouldBe(1.5);
		options.TimeOfDay.OffHoursMultiplier.ShouldBe(0.6);
	}

	[Fact]
	public void SupportCombinedConfiguration()
	{
		var options = new RuleBasedTtlOptions
		{
			// Inherited from AdaptiveTtlOptions
			MinTtl = TimeSpan.FromSeconds(10),
			MaxTtl = TimeSpan.FromHours(12),
			TargetHitRate = 0.85,
			// RuleBasedTtlOptions sub-options
			HitRate = new RuleBasedHitRateOptions { HighHitRateThreshold = 0.95 },
			Frequency = new RuleBasedFrequencyOptions { HighFrequencyThreshold = 500 },
			Load = new RuleBasedLoadOptions { ExpensiveMissMultiplier = 1.5 },
			TimeOfDay = new RuleBasedTimeOfDayOptions { PeakHoursStart = 7 },
			Content = new RuleBasedContentOptions { LargeContentThreshold = 2 * 1024 * 1024 },
		};

		// Verify inherited properties
		options.MinTtl.ShouldBe(TimeSpan.FromSeconds(10));
		options.MaxTtl.ShouldBe(TimeSpan.FromHours(12));
		options.TargetHitRate.ShouldBe(0.85);

		// Verify sub-option properties
		options.HitRate.HighHitRateThreshold.ShouldBe(0.95);
		options.Frequency.HighFrequencyThreshold.ShouldBe(500);
		options.Load.ExpensiveMissMultiplier.ShouldBe(1.5);
		options.TimeOfDay.PeakHoursStart.ShouldBe(7);
		options.Content.LargeContentThreshold.ShouldBe(2 * 1024 * 1024);
	}

	#endregion

	#region No Stale Shims

	[Fact]
	public void RuleBasedTtlOptions_NotContainObsoleteProperties()
	{
		var allProperties = typeof(RuleBasedTtlOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		foreach (var prop in allProperties)
		{
			var obsoleteAttr = prop.GetCustomAttribute<ObsoleteAttribute>();
			obsoleteAttr.ShouldBeNull(
				$"RuleBasedTtlOptions.{prop.Name} still has [Obsolete] shim — shim removal incomplete");
		}
	}

	[Fact]
	public void AdaptiveTtlOptions_NotContainObsoleteProperties()
	{
		var allProperties = typeof(AdaptiveTtlOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		foreach (var prop in allProperties)
		{
			var obsoleteAttr = prop.GetCustomAttribute<ObsoleteAttribute>();
			obsoleteAttr.ShouldBeNull(
				$"AdaptiveTtlOptions.{prop.Name} still has [Obsolete] shim — shim removal incomplete");
		}
	}

	#endregion

	#region Inheritance Verification

	[Fact]
	public void RuleBasedTtlOptions_InheritsFromAdaptiveTtlOptions()
	{
		typeof(RuleBasedTtlOptions).BaseType.ShouldBe(typeof(AdaptiveTtlOptions));
	}

	[Fact]
	public void RuleBasedTtlOptions_InheritsBaseProperties()
	{
		var options = new RuleBasedTtlOptions();

		// Verify inherited AdaptiveTtlOptions properties work
		options.MinTtl.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaxTtl.ShouldBe(TimeSpan.FromHours(24));
		options.TargetHitRate.ShouldBe(0.8);
		options.Weights.ShouldNotBeNull();
		options.Thresholds.ShouldNotBeNull();
	}

	#endregion
}
