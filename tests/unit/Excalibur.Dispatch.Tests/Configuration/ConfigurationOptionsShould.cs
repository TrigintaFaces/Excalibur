// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Configuration;

using ConfigOutboxOptions = Excalibur.Dispatch.Options.Configuration.OutboxConfigurationOptions;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ConfigurationOptionsShould
{
	// --- DispatchOptions ---

	[Fact]
	public void DispatchOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new DispatchOptions();

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.MaxConcurrency.ShouldBe(Environment.ProcessorCount * 2);
		options.UseLightMode.ShouldBeFalse();
		options.MessageBufferSize.ShouldBe(1024);
		options.EnablePipelineSynthesis.ShouldBeTrue();
		options.Features.ShouldNotBeNull();
		options.Inbox.ShouldNotBeNull();
		options.Outbox.ShouldNotBeNull();
		options.Consumer.ShouldNotBeNull();
		options.CrossCutting.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new DispatchOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(2),
			MaxConcurrency = 16,
			UseLightMode = true,
			MessageBufferSize = 2048,
			EnablePipelineSynthesis = false,
		};

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.MaxConcurrency.ShouldBe(16);
		options.UseLightMode.ShouldBeTrue();
		options.MessageBufferSize.ShouldBe(2048);
		options.EnablePipelineSynthesis.ShouldBeFalse();
	}

	// --- DispatchFeatureOptions ---

	[Fact]
	public void DispatchFeatureOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new DispatchFeatureOptions();

		// Assert
		options.EnableCorrelation.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.EnableStructuredLogging.ShouldBeTrue();
		options.ValidateMessageSchemas.ShouldBeTrue();
		options.EnableMultiTenancy.ShouldBeFalse();
		options.EnableVersioning.ShouldBeTrue();
		options.EnableAuthorization.ShouldBeTrue();
		options.EnableTransactions.ShouldBeFalse();
	}

	[Fact]
	public void DispatchFeatureOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new DispatchFeatureOptions
		{
			EnableCorrelation = false,
			EnableMetrics = false,
			EnableStructuredLogging = false,
			ValidateMessageSchemas = false,
			EnableMultiTenancy = true,
			EnableVersioning = false,
			EnableAuthorization = false,
			EnableTransactions = true,
		};

		// Assert
		options.EnableCorrelation.ShouldBeFalse();
		options.EnableMetrics.ShouldBeFalse();
		options.EnableStructuredLogging.ShouldBeFalse();
		options.ValidateMessageSchemas.ShouldBeFalse();
		options.EnableMultiTenancy.ShouldBeTrue();
		options.EnableVersioning.ShouldBeFalse();
		options.EnableAuthorization.ShouldBeFalse();
		options.EnableTransactions.ShouldBeTrue();
	}

	// --- DispatchCrossCuttingOptions ---

	// --- CachingOptions ---

	// --- ConsumerOptions ---

	[Fact]
	public void ConsumerOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new ConsumerOptions();

		// Assert
		options.Dedupe.ShouldNotBeNull();
		options.AckAfterHandle.ShouldBeTrue();
		options.MaxConcurrentMessages.ShouldBe(10);
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.MaxRetries.ShouldBe(3);
	}

	[Fact]
	public void ConsumerOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new ConsumerOptions
		{
			AckAfterHandle = false,
			MaxConcurrentMessages = 20,
			VisibilityTimeout = TimeSpan.FromMinutes(10),
			MaxRetries = 5,
		};

		// Assert
		options.AckAfterHandle.ShouldBeFalse();
		options.MaxConcurrentMessages.ShouldBe(20);
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(10));
		options.MaxRetries.ShouldBe(5);
	}

	// --- InboxOptions ---

	[Fact]
	public void InboxOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new InboxConfigurationOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.DeduplicationExpiryHours.ShouldBe(24);
		options.AckAfterHandle.ShouldBeTrue();
		options.MaxRetries.ShouldBe(3);
		options.RetryDelayMinutes.ShouldBe(5);
		options.MaxRetention.ShouldBe(TimeSpan.FromDays(7));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
		options.CleanupIntervalSeconds.ShouldBe(3600);
		options.RetentionDays.ShouldBe(7);
	}

	[Fact]
	public void InboxOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new InboxConfigurationOptions
		{
			Enabled = true,
			DeduplicationExpiryHours = 48,
			AckAfterHandle = false,
			MaxRetries = 5,
			RetryDelayMinutes = 10,
			MaxRetention = TimeSpan.FromDays(14),
			CleanupInterval = TimeSpan.FromHours(6),
			CleanupIntervalSeconds = 7200,
			RetentionDays = 14,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.DeduplicationExpiryHours.ShouldBe(48);
		options.AckAfterHandle.ShouldBeFalse();
		options.MaxRetries.ShouldBe(5);
		options.RetryDelayMinutes.ShouldBe(10);
		options.MaxRetention.ShouldBe(TimeSpan.FromDays(14));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(6));
		options.CleanupIntervalSeconds.ShouldBe(7200);
		options.RetentionDays.ShouldBe(14);
	}

	// --- ObservabilityOptions ---

	// --- OutboxOptions (Configuration) ---

	[Fact]
	public void PerformanceOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new PerformanceOptions();

		// Assert
		options.EnableTypeMetadataCaching.ShouldBeTrue();
		options.MessagePoolSize.ShouldBe(1000);
		options.UseAllocationFreeExecution.ShouldBeTrue();
		options.AutoFreezeOnStart.ShouldBeTrue();
	}

	[Fact]
	public void PerformanceOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new PerformanceOptions
		{
			EnableTypeMetadataCaching = false,
			MessagePoolSize = 500,
			UseAllocationFreeExecution = false,
			AutoFreezeOnStart = false,
		};

		// Assert
		options.EnableTypeMetadataCaching.ShouldBeFalse();
		options.MessagePoolSize.ShouldBe(500);
		options.UseAllocationFreeExecution.ShouldBeFalse();
		options.AutoFreezeOnStart.ShouldBeFalse();
	}

	// --- ResilienceOptions (Configuration) ---

}
