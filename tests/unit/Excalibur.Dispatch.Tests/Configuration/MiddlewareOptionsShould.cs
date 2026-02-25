// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Transactions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Options;

using MiddlewareOutboxOptions = Excalibur.Dispatch.Options.Middleware.OutboxOptions;
using MiddlewareTimeoutOptions = Excalibur.Dispatch.Options.Middleware.TimeoutOptions;
using MiddlewareTransactionOptions = Excalibur.Dispatch.Options.Middleware.TransactionOptions;
using MiddlewareValidationOptions = Excalibur.Dispatch.Options.Middleware.ValidationOptions;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MiddlewareOptionsShould
{
	// --- AuditLoggingOptions ---

	[Fact]
	public void AuditLoggingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new AuditLoggingOptions();

		// Assert
		options.LogMessagePayload.ShouldBeFalse();
		options.MaxPayloadSize.ShouldBe(10_000);
		options.MaxPayloadDepth.ShouldBe(5);
		options.UserIdExtractor.ShouldBeNull();
		options.CorrelationIdExtractor.ShouldBeNull();
		options.PayloadFilter.ShouldBeNull();
		options.IncludeSensitiveData.ShouldBeFalse();
	}

	[Fact]
	public void AuditLoggingOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new AuditLoggingOptions
		{
			LogMessagePayload = true,
			MaxPayloadSize = 5000,
			MaxPayloadDepth = 3,
			UserIdExtractor = _ => "user-1",
			CorrelationIdExtractor = _ => "corr-1",
			PayloadFilter = _ => true,
			IncludeSensitiveData = true,
		};

		// Assert
		options.LogMessagePayload.ShouldBeTrue();
		options.MaxPayloadSize.ShouldBe(5000);
		options.MaxPayloadDepth.ShouldBe(3);
		options.UserIdExtractor.ShouldNotBeNull();
		options.CorrelationIdExtractor.ShouldNotBeNull();
		options.PayloadFilter.ShouldNotBeNull();
		options.IncludeSensitiveData.ShouldBeTrue();
	}

	// --- AuthenticationOptions ---

	[Fact]
	public void AuthenticationOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new AuthenticationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RequireAuthentication.ShouldBeTrue();
		options.DefaultScheme.ShouldBe("Bearer");
		options.TokenHeader.ShouldBe("Authorization");
		options.EnableCaching.ShouldBeTrue();
		options.CacheDuration.ShouldBe(TimeSpan.FromMinutes(5));
		options.MaxCacheSize.ShouldBe(1000);
		options.ValidApiKeys.ShouldBeNull();
		options.AllowAnonymousForTypes.ShouldBeNull();
	}

	// --- AuthorizationOptions ---

	[Fact]
	public void AuthorizationOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new AuthorizationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.AllowAnonymousAccess.ShouldBeFalse();
		options.BypassAuthorizationForTypes.ShouldBeNull();
		options.DefaultPolicyName.ShouldBe("Default");
	}

	// --- ContractVersionCheckOptions ---

	[Fact]
	public void ContractVersionCheckOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RequireExplicitVersions.ShouldBeFalse();
		options.FailOnIncompatibleVersions.ShouldBeTrue();
		options.FailOnUnknownVersions.ShouldBeFalse();
		options.RecordDeprecationMetrics.ShouldBeTrue();
		options.Headers.ShouldNotBeNull();
		options.SupportedVersions.ShouldBeNull();
		options.BypassVersionCheckForTypes.ShouldBeNull();
	}

	// --- VersionCheckHeaders ---

	[Fact]
	public void VersionCheckHeaders_DefaultValues_AreCorrect()
	{
		// Act
		var headers = new VersionCheckHeaders();

		// Assert
		headers.VersionHeaderName.ShouldBe("X-Message-Version");
		headers.SchemaIdHeaderName.ShouldBe("X-Schema-MessageId");
		headers.ProducerVersionHeaderName.ShouldBe("X-Producer-Version");
		headers.ProducerServiceHeaderName.ShouldBe("X-Producer-Service");
	}

	// --- InputSanitizationOptions ---

	[Fact]
	public void InputSanitizationOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new InputSanitizationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.Features.ShouldNotBeNull();
		options.MaxStringLength.ShouldBe(0);
		options.SanitizeContextItems.ShouldBeTrue();
		options.UseCustomSanitization.ShouldBeTrue();
		options.ThrowOnSanitizationError.ShouldBeFalse();
		options.BypassSanitizationForTypes.ShouldBeNull();
		options.ExcludeProperties.ShouldBeNull();
	}

	// --- SanitizationFeatures ---

	[Fact]
	public void SanitizationFeatures_DefaultValues_AreCorrect()
	{
		// Act
		var features = new SanitizationFeatures();

		// Assert
		features.PreventXss.ShouldBeTrue();
		features.RemoveHtmlTags.ShouldBeTrue();
		features.PreventSqlInjection.ShouldBeTrue();
		features.PreventPathTraversal.ShouldBeTrue();
		features.RemoveNullBytes.ShouldBeTrue();
		features.NormalizeUnicode.ShouldBeTrue();
		features.TrimWhitespace.ShouldBeTrue();
	}

	[Fact]
	public void SanitizationFeatures_AllProperties_AreSettable()
	{
		// Act
		var features = new SanitizationFeatures
		{
			PreventXss = false,
			RemoveHtmlTags = false,
			PreventSqlInjection = false,
			PreventPathTraversal = false,
			RemoveNullBytes = false,
			NormalizeUnicode = false,
			TrimWhitespace = false,
		};

		// Assert
		features.PreventXss.ShouldBeFalse();
		features.RemoveHtmlTags.ShouldBeFalse();
		features.PreventSqlInjection.ShouldBeFalse();
		features.PreventPathTraversal.ShouldBeFalse();
		features.RemoveNullBytes.ShouldBeFalse();
		features.NormalizeUnicode.ShouldBeFalse();
		features.TrimWhitespace.ShouldBeFalse();
	}

	// --- LoggingMiddlewareOptions ---

	[Fact]
	public void LoggingMiddlewareOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new LoggingMiddlewareOptions();

		// Assert
		options.SuccessLevel.ShouldBe(LogLevel.Information);
		options.FailureLevel.ShouldBe(LogLevel.Error);
		options.IncludePayload.ShouldBeFalse();
		options.IncludeTiming.ShouldBeTrue();
		options.ExcludeTypes.ShouldNotBeNull();
		options.ExcludeTypes.ShouldBeEmpty();
		options.LogStart.ShouldBeTrue();
		options.LogCompletion.ShouldBeTrue();
	}

	// --- MetricsLoggingOptions ---

	[Fact]
	public void MetricsLoggingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MetricsLoggingOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RecordOpenTelemetryMetrics.ShouldBeTrue();
		options.RecordCustomMetrics.ShouldBeTrue();
		options.LogProcessingDetails.ShouldBeTrue();
		options.SlowOperationThreshold.ShouldBe(TimeSpan.FromSeconds(1));
		options.BypassMetricsForTypes.ShouldBeNull();
		options.IncludeMessageSizes.ShouldBeTrue();
		options.SampleRate.ShouldBe(1.0);
	}

	// --- OutboxOptions (Middleware) ---

	[Fact]
	public void MiddlewareOutboxOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MiddlewareOutboxOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.DefaultPriority.ShouldBe(0);
		options.ContinueOnStagingError.ShouldBeFalse();
		options.BypassOutboxForTypes.ShouldBeNull();
		options.PublishBatchSize.ShouldBe(100);
		options.PublishPollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaxRetries.ShouldBe(3);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
		options.EnableExponentialRetryBackoff.ShouldBeFalse();
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromMinutes(30));
		options.CleanupAge.ShouldBe(TimeSpan.FromDays(7));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
		options.EnableAdaptivePolling.ShouldBeFalse();
		options.MinPollingInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.AdaptivePollingBackoffMultiplier.ShouldBe(2.0);
	}

	// --- OutboxOptionsValidator ---

	[Fact]
	public void OutboxOptionsValidator_Valid_ReturnsSuccess()
	{
		// Arrange
		var validator = new OutboxOptionsValidator();
		var options = new MiddlewareOutboxOptions();

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void OutboxOptionsValidator_ThrowsOnNull()
	{
		// Arrange
		var validator = new OutboxOptionsValidator();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => validator.Validate(null, null!));
	}

	[Fact]
	public void OutboxOptionsValidator_DetectsZeroBatchSize()
	{
		// Arrange
		var validator = new OutboxOptionsValidator();
		var options = new MiddlewareOutboxOptions { PublishBatchSize = 0 };

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("PublishBatchSize");
	}

	[Fact]
	public void OutboxOptionsValidator_DetectsZeroPollingInterval()
	{
		// Arrange
		var validator = new OutboxOptionsValidator();
		var options = new MiddlewareOutboxOptions { PublishPollingInterval = TimeSpan.Zero };

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("PublishPollingInterval");
	}

	[Fact]
	public void OutboxOptionsValidator_DetectsNegativeMaxRetries()
	{
		// Arrange
		var validator = new OutboxOptionsValidator();
		var options = new MiddlewareOutboxOptions { MaxRetries = -1 };

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxRetries");
	}

	[Fact]
	public void OutboxOptionsValidator_DetectsZeroRetryDelay()
	{
		// Arrange
		var validator = new OutboxOptionsValidator();
		var options = new MiddlewareOutboxOptions { RetryDelay = TimeSpan.Zero };

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RetryDelay");
	}

	[Fact]
	public void OutboxOptionsValidator_DetectsMaxRetryDelayLessThanRetryDelay_WhenExponential()
	{
		// Arrange
		var validator = new OutboxOptionsValidator();
		var options = new MiddlewareOutboxOptions
		{
			EnableExponentialRetryBackoff = true,
			RetryDelay = TimeSpan.FromMinutes(10),
			MaxRetryDelay = TimeSpan.FromMinutes(5),
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxRetryDelay");
	}

	[Fact]
	public void OutboxOptionsValidator_DetectsMinPollingGreaterThanMax_WhenAdaptive()
	{
		// Arrange
		var validator = new OutboxOptionsValidator();
		var options = new MiddlewareOutboxOptions
		{
			EnableAdaptivePolling = true,
			MinPollingInterval = TimeSpan.FromSeconds(10),
			PublishPollingInterval = TimeSpan.FromSeconds(5),
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MinPollingInterval");
	}

	[Fact]
	public void OutboxOptionsValidator_DetectsBackoffMultiplierTooLow_WhenAdaptive()
	{
		// Arrange
		var validator = new OutboxOptionsValidator();
		var options = new MiddlewareOutboxOptions
		{
			EnableAdaptivePolling = true,
			AdaptivePollingBackoffMultiplier = 0.5,
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("AdaptivePollingBackoffMultiplier");
	}

	// --- OutboxStagingOptions ---

	[Fact]
	public void OutboxStagingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new OutboxStagingOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.MaxOutboundMessagesPerOperation.ShouldBe(100);
		options.CompressMessageData.ShouldBeFalse();
		options.BypassOutboxForTypes.ShouldBeNull();
	}

	// --- RateLimitingOptions ---

	[Fact]
	public void RateLimitingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new RateLimitingOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.EnablePerTenantLimiting.ShouldBeTrue();
		options.DefaultLimit.ShouldNotBeNull();
		options.DefaultLimit.Algorithm.ShouldBe(RateLimitAlgorithm.TokenBucket);
		options.DefaultLimit.TokenLimit.ShouldBe(100);
		options.GlobalLimit.ShouldNotBeNull();
		options.GlobalLimit.TokenLimit.ShouldBe(1000);
		options.MessageTypeLimits.ShouldNotBeNull();
		options.MessageTypeLimits.ShouldBeEmpty();
		options.BypassRateLimitingForTypes.ShouldBeNull();
	}

	// --- TenantIdentityOptions ---

	[Fact]
	public void TenantIdentityOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new TenantIdentityOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.ValidateTenantAccess.ShouldBeTrue();
		options.TenantIdHeader.ShouldBe("X-Tenant-ID");
		options.TenantNameHeader.ShouldBe("X-Tenant-Name");
		options.TenantRegionHeader.ShouldBe("X-Tenant-Region");
		options.DefaultTenantId.ShouldBe(TenantDefaults.DefaultTenantId);
		options.MinTenantIdLength.ShouldBe(1);
		options.MaxTenantIdLength.ShouldBe(100);
		options.TenantIdPattern.ShouldBeNull();
	}

	// --- TimeoutOptions (Middleware) ---

	[Fact]
	public void TimeoutOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MiddlewareTimeoutOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.ActionTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.EventTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.DocumentTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		options.MessageTypeTimeouts.ShouldNotBeNull();
		options.MessageTypeTimeouts.ShouldBeEmpty();
		options.ThrowOnTimeout.ShouldBeTrue();
	}

	[Fact]
	public void TimeoutOptions_Validate_Succeeds_WhenValid()
	{
		// Arrange
		var options = new MiddlewareTimeoutOptions();

		// Act & Assert - should not throw
		options.Validate();
	}

	[Fact]
	public void TimeoutOptions_Validate_ThrowsOnZeroDefaultTimeout()
	{
		// Arrange
		var options = new MiddlewareTimeoutOptions
		{
			DefaultTimeout = TimeSpan.Zero,
		};

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void TimeoutOptions_Validate_ThrowsOnZeroActionTimeout()
	{
		// Arrange
		var options = new MiddlewareTimeoutOptions
		{
			ActionTimeout = TimeSpan.Zero,
		};

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void TimeoutOptions_Validate_ThrowsOnZeroEventTimeout()
	{
		// Arrange
		var options = new MiddlewareTimeoutOptions
		{
			EventTimeout = TimeSpan.Zero,
		};

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void TimeoutOptions_Validate_ThrowsOnZeroDocumentTimeout()
	{
		// Arrange
		var options = new MiddlewareTimeoutOptions
		{
			DocumentTimeout = TimeSpan.Zero,
		};

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void TimeoutOptions_Validate_ThrowsOnZeroMessageTypeTimeout()
	{
		// Arrange
		var options = new MiddlewareTimeoutOptions();
		options.MessageTypeTimeouts["TestMessage"] = TimeSpan.Zero;

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	// --- TransactionOptions ---

	[Fact]
	public void TransactionOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MiddlewareTransactionOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RequireTransactionByDefault.ShouldBeTrue();
		options.EnableDistributedTransactions.ShouldBeFalse();
		options.DefaultIsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.BypassTransactionForTypes.ShouldBeNull();
	}

	// --- UnifiedBatchingOptions ---

	[Fact]
	public void UnifiedBatchingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new UnifiedBatchingOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(32);
		options.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(250));
		options.MaxParallelism.ShouldBe(Environment.ProcessorCount);
		options.ProcessAsOptimizedBulk.ShouldBeTrue();
		options.NonBatchableMessageTypes.ShouldNotBeNull();
		options.NonBatchableMessageTypes.ShouldBeEmpty();
		options.BatchFilter.ShouldBeNull();
		options.BatchKeySelector.ShouldBeNull();
	}

	// --- ValidationOptions ---

	[Fact]
	public void ValidationOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MiddlewareValidationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.UseDataAnnotations.ShouldBeTrue();
		options.UseCustomValidation.ShouldBeTrue();
		options.StopOnFirstError.ShouldBeFalse();
		options.BypassValidationForTypes.ShouldBeNull();
	}
}
