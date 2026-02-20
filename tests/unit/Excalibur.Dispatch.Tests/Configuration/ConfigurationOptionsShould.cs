// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Configuration;

using ConfigOutboxOptions = Excalibur.Dispatch.Options.Configuration.OutboxOptions;
using ConfigResilienceOptions = Excalibur.Dispatch.Options.Configuration.ResilienceOptions;
using ConfigSecurityOptions = Excalibur.Dispatch.Options.Configuration.SecurityOptions;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
		options.EnableCacheMiddleware.ShouldBeTrue();
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
			EnableCacheMiddleware = false,
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
		options.EnableCacheMiddleware.ShouldBeFalse();
		options.EnableMultiTenancy.ShouldBeTrue();
		options.EnableVersioning.ShouldBeFalse();
		options.EnableAuthorization.ShouldBeFalse();
		options.EnableTransactions.ShouldBeTrue();
	}

	// --- DispatchCrossCuttingOptions ---

	[Fact]
	public void DispatchCrossCuttingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new DispatchCrossCuttingOptions();

		// Assert
		options.DefaultRetryPolicy.ShouldNotBeNull();
		options.Performance.ShouldNotBeNull();
		options.Security.ShouldNotBeNull();
		options.Observability.ShouldNotBeNull();
		options.Resilience.ShouldNotBeNull();
		options.Caching.ShouldNotBeNull();
	}

	// --- RetryPolicy ---

	[Fact]
	public void RetryPolicy_DefaultValues_AreCorrect()
	{
		// Act
		var policy = new RetryPolicy();

		// Assert
		policy.MaxAttempts.ShouldBe(3);
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(1));
		policy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(1));
		policy.BackoffMultiplier.ShouldBe(2.0);
		policy.UseExponentialBackoff.ShouldBeTrue();
	}

	[Fact]
	public void RetryPolicy_AllProperties_AreSettable()
	{
		// Act
		var policy = new RetryPolicy
		{
			MaxAttempts = 5,
			InitialDelay = TimeSpan.FromSeconds(2),
			MaxDelay = TimeSpan.FromMinutes(5),
			BackoffMultiplier = 3.0,
			UseExponentialBackoff = false,
		};

		// Assert
		policy.MaxAttempts.ShouldBe(5);
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(2));
		policy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(5));
		policy.BackoffMultiplier.ShouldBe(3.0);
		policy.UseExponentialBackoff.ShouldBeFalse();
	}

	// --- CachingOptions ---

	[Fact]
	public void CachingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new CachingOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void CachingOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new CachingOptions
		{
			Enabled = true,
			DefaultExpiration = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(30));
	}

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

	// --- DeduplicationOptions ---

	[Fact]
	public void DeduplicationOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new DeduplicationOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.ExpiryHours.ShouldBe(24);
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(5));
		options.WindowSeconds.ShouldBe(300);
		options.MaxCacheSize.ShouldBe(10000);
	}

	[Fact]
	public void DeduplicationOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new DeduplicationOptions
		{
			Enabled = true,
			ExpiryHours = 48,
			CleanupInterval = TimeSpan.FromMinutes(10),
			WindowSeconds = 600,
			MaxCacheSize = 50000,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.ExpiryHours.ShouldBe(48);
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(10));
		options.WindowSeconds.ShouldBe(600);
		options.MaxCacheSize.ShouldBe(50000);
	}

	// --- InboxOptions ---

	[Fact]
	public void InboxOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new InboxOptions();

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
		var options = new InboxOptions
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

	[Fact]
	public void ObservabilityOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new ObservabilityOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.EnableTracing.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.EnableContextFlow.ShouldBeTrue();
	}

	[Fact]
	public void ObservabilityOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new ObservabilityOptions
		{
			Enabled = false,
			EnableTracing = false,
			EnableMetrics = false,
			EnableContextFlow = false,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.EnableTracing.ShouldBeFalse();
		options.EnableMetrics.ShouldBeFalse();
		options.EnableContextFlow.ShouldBeFalse();
	}

	// --- OutboxOptions (Configuration) ---

	[Fact]
	public void ConfigOutboxOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new ConfigOutboxOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.BatchSize.ShouldBe(100);
		options.PublishIntervalMs.ShouldBe(1000);
		options.MaxRetries.ShouldBe(3);
		options.SentMessageRetention.ShouldBe(TimeSpan.FromDays(1));
		options.UseInMemoryStorage.ShouldBeFalse();
	}

	[Fact]
	public void ConfigOutboxOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new ConfigOutboxOptions
		{
			Enabled = false,
			BatchSize = 50,
			PublishIntervalMs = 2000,
			MaxRetries = 5,
			SentMessageRetention = TimeSpan.FromDays(7),
			UseInMemoryStorage = true,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.BatchSize.ShouldBe(50);
		options.PublishIntervalMs.ShouldBe(2000);
		options.MaxRetries.ShouldBe(5);
		options.SentMessageRetention.ShouldBe(TimeSpan.FromDays(7));
		options.UseInMemoryStorage.ShouldBeTrue();
	}

	// --- PerformanceOptions ---

	[Fact]
	public void PerformanceOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new PerformanceOptions();

		// Assert
		options.EnableCacheMiddleware.ShouldBeTrue();
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
			EnableCacheMiddleware = false,
			EnableTypeMetadataCaching = false,
			MessagePoolSize = 500,
			UseAllocationFreeExecution = false,
			AutoFreezeOnStart = false,
		};

		// Assert
		options.EnableCacheMiddleware.ShouldBeFalse();
		options.EnableTypeMetadataCaching.ShouldBeFalse();
		options.MessagePoolSize.ShouldBe(500);
		options.UseAllocationFreeExecution.ShouldBeFalse();
		options.AutoFreezeOnStart.ShouldBeFalse();
	}

	// --- ResilienceOptions (Configuration) ---

	[Fact]
	public void ConfigResilienceOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new ConfigResilienceOptions();

		// Assert
		options.DefaultRetryCount.ShouldBe(3);
		options.EnableCircuitBreaker.ShouldBeFalse();
		options.EnableTimeout.ShouldBeFalse();
		options.EnableBulkhead.ShouldBeFalse();
	}

	[Fact]
	public void ConfigResilienceOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new ConfigResilienceOptions
		{
			DefaultRetryCount = 5,
			EnableCircuitBreaker = true,
			EnableTimeout = true,
			EnableBulkhead = true,
		};

		// Assert
		options.DefaultRetryCount.ShouldBe(5);
		options.EnableCircuitBreaker.ShouldBeTrue();
		options.EnableTimeout.ShouldBeTrue();
		options.EnableBulkhead.ShouldBeTrue();
	}

	// --- SecurityOptions (Configuration) ---

	[Fact]
	public void ConfigSecurityOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new ConfigSecurityOptions();

		// Assert
		options.EnableEncryption.ShouldBeFalse();
		options.EnableSigning.ShouldBeFalse();
		options.EnableRateLimiting.ShouldBeFalse();
		options.EnableValidation.ShouldBeTrue();
	}

	[Fact]
	public void ConfigSecurityOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new ConfigSecurityOptions
		{
			EnableEncryption = true,
			EnableSigning = true,
			EnableRateLimiting = true,
			EnableValidation = false,
		};

		// Assert
		options.EnableEncryption.ShouldBeTrue();
		options.EnableSigning.ShouldBeTrue();
		options.EnableRateLimiting.ShouldBeTrue();
		options.EnableValidation.ShouldBeFalse();
	}
}
