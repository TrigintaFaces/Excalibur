using System.Transactions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Versioning;
using Excalibur.Dispatch.Middleware.Validation;
using Excalibur.Dispatch.Middleware.Transaction;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Middleware.Logging;
using Excalibur.Dispatch.Middleware.Batch;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Middleware;

using MiddlewareTimeoutOptions = Excalibur.Dispatch.Options.Middleware.TimeoutOptions;
using MiddlewareTransactionOptions = Excalibur.Dispatch.Options.Middleware.TransactionOptions;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MiddlewareOptionsShould
{
	// --- AuditLoggingOptions ---

	[Fact]
	public void AuditLoggingOptions_HaveDefaults()
	{
		var opts = new AuditLoggingOptions();

		opts.LogMessagePayload.ShouldBeFalse();
		opts.MaxPayloadSize.ShouldBe(10_000);
		opts.MaxPayloadDepth.ShouldBe(5);
		opts.UserIdExtractor.ShouldBeNull();
		opts.CorrelationIdExtractor.ShouldBeNull();
		opts.PayloadFilter.ShouldBeNull();
		opts.IncludeSensitiveData.ShouldBeFalse();
	}

	[Fact]
	public void AuditLoggingOptions_AllowSettingProperties()
	{
		var opts = new AuditLoggingOptions
		{
			LogMessagePayload = true,
			MaxPayloadSize = 5000,
			MaxPayloadDepth = 3,
			IncludeSensitiveData = true,
		};

		opts.LogMessagePayload.ShouldBeTrue();
		opts.MaxPayloadSize.ShouldBe(5000);
		opts.MaxPayloadDepth.ShouldBe(3);
		opts.IncludeSensitiveData.ShouldBeTrue();
	}

	// --- AuthenticationOptions ---

	[Fact]
	public void AuthenticationOptions_HaveDefaults()
	{
		var opts = new AuthenticationOptions();

		opts.Enabled.ShouldBeTrue();
		opts.RequireAuthentication.ShouldBeTrue();
		opts.DefaultScheme.ShouldBe("Bearer");
		opts.TokenHeader.ShouldBe("Authorization");
		opts.EnableCaching.ShouldBeTrue();
		opts.CacheDuration.ShouldBe(TimeSpan.FromMinutes(5));
		opts.MaxCacheSize.ShouldBe(1000);
		opts.AllowAnonymousForTypes.ShouldBeNull();
	}

	[Fact]
	public void AuthenticationOptions_AllowSettingProperties()
	{
		var opts = new AuthenticationOptions
		{
			Enabled = false,
			RequireAuthentication = false,
			DefaultScheme = "ApiKey",
			TokenHeader = "X-Api-Key",
			EnableCaching = false,
			CacheDuration = TimeSpan.FromMinutes(10),
			MaxCacheSize = 500,
			AllowAnonymousForTypes = ["HealthCheck"],
		};

		opts.Enabled.ShouldBeFalse();
		opts.RequireAuthentication.ShouldBeFalse();
		opts.DefaultScheme.ShouldBe("ApiKey");
		opts.TokenHeader.ShouldBe("X-Api-Key");
		opts.EnableCaching.ShouldBeFalse();
		opts.CacheDuration.ShouldBe(TimeSpan.FromMinutes(10));
		opts.MaxCacheSize.ShouldBe(500);
		opts.AllowAnonymousForTypes.ShouldContain("HealthCheck");
	}

	// --- AuthorizationOptions ---

	[Fact]
	public void AuthorizationOptions_HaveDefaults()
	{
		var opts = new AuthorizationOptions();

		opts.Enabled.ShouldBeTrue();
		opts.AllowAnonymousAccess.ShouldBeFalse();
		opts.BypassAuthorizationForTypes.ShouldBeNull();
		opts.DefaultPolicyName.ShouldBe("Default");
	}

	[Fact]
	public void AuthorizationOptions_AllowSettingProperties()
	{
		var opts = new AuthorizationOptions
		{
			Enabled = false,
			AllowAnonymousAccess = true,
			BypassAuthorizationForTypes = ["HealthCheck"],
			DefaultPolicyName = "Admin",
		};

		opts.Enabled.ShouldBeFalse();
		opts.AllowAnonymousAccess.ShouldBeTrue();
		opts.BypassAuthorizationForTypes.ShouldContain("HealthCheck");
		opts.DefaultPolicyName.ShouldBe("Admin");
	}

	// --- ContractVersionCheckOptions ---

	[Fact]
	public void ContractVersionCheckOptions_HaveDefaults()
	{
		var opts = new ContractVersionCheckOptions();

		opts.Enabled.ShouldBeTrue();
		opts.RequireExplicitVersions.ShouldBeFalse();
		opts.FailOnIncompatibleVersions.ShouldBeTrue();
		opts.FailOnUnknownVersions.ShouldBeFalse();
		opts.RecordDeprecationMetrics.ShouldBeTrue();
		opts.Headers.ShouldNotBeNull();
		opts.SupportedVersions.ShouldBeNull();
		opts.BypassVersionCheckForTypes.ShouldBeNull();
	}

	// --- InputSanitizationOptions ---

	[Fact]
	public void InputSanitizationOptions_HaveDefaults()
	{
		var opts = new InputSanitizationOptions();

		opts.Enabled.ShouldBeTrue();
		opts.Features.ShouldNotBeNull();
		opts.MaxStringLength.ShouldBe(0);
		opts.SanitizeContextItems.ShouldBeTrue();
		opts.UseCustomSanitization.ShouldBeTrue();
		opts.ThrowOnSanitizationError.ShouldBeFalse();
		opts.BypassSanitizationForTypes.ShouldBeNull();
		opts.ExcludeProperties.ShouldBeNull();
	}

	// --- SanitizationFeatures ---

	[Fact]
	public void SanitizationFeatures_HaveDefaults()
	{
		var f = new SanitizationFeatures();

		f.PreventXss.ShouldBeTrue();
		f.RemoveHtmlTags.ShouldBeTrue();
		f.PreventSqlInjection.ShouldBeTrue();
		f.PreventPathTraversal.ShouldBeTrue();
		f.RemoveNullBytes.ShouldBeTrue();
		f.NormalizeUnicode.ShouldBeTrue();
		f.TrimWhitespace.ShouldBeTrue();
	}

	[Fact]
	public void SanitizationFeatures_AllowDisabling()
	{
		var f = new SanitizationFeatures
		{
			PreventXss = false,
			RemoveHtmlTags = false,
			PreventSqlInjection = false,
			PreventPathTraversal = false,
			RemoveNullBytes = false,
			NormalizeUnicode = false,
			TrimWhitespace = false,
		};

		f.PreventXss.ShouldBeFalse();
		f.RemoveHtmlTags.ShouldBeFalse();
		f.PreventSqlInjection.ShouldBeFalse();
		f.PreventPathTraversal.ShouldBeFalse();
		f.RemoveNullBytes.ShouldBeFalse();
		f.NormalizeUnicode.ShouldBeFalse();
		f.TrimWhitespace.ShouldBeFalse();
	}

	// --- LoggingMiddlewareOptions ---

	[Fact]
	public void LoggingMiddlewareOptions_HaveDefaults()
	{
		var opts = new LoggingMiddlewareOptions();

		opts.SuccessLevel.ShouldBe(LogLevel.Information);
		opts.FailureLevel.ShouldBe(LogLevel.Error);
		opts.IncludePayload.ShouldBeFalse();
		opts.IncludeTiming.ShouldBeTrue();
		opts.ExcludeTypes.ShouldBeEmpty();
		opts.LogStart.ShouldBeTrue();
		opts.LogCompletion.ShouldBeTrue();
	}

	[Fact]
	public void LoggingMiddlewareOptions_AllowSettingProperties()
	{
		var opts = new LoggingMiddlewareOptions
		{
			SuccessLevel = LogLevel.Debug,
			FailureLevel = LogLevel.Critical,
			IncludePayload = true,
			IncludeTiming = false,
			LogStart = false,
			LogCompletion = false,
		};

		opts.SuccessLevel.ShouldBe(LogLevel.Debug);
		opts.FailureLevel.ShouldBe(LogLevel.Critical);
		opts.IncludePayload.ShouldBeTrue();
		opts.IncludeTiming.ShouldBeFalse();
		opts.LogStart.ShouldBeFalse();
		opts.LogCompletion.ShouldBeFalse();
	}

	// --- MetricsLoggingOptions ---

	[Fact]
	public void MetricsLoggingOptions_HaveDefaults()
	{
		var opts = new MetricsLoggingOptions();

		opts.Enabled.ShouldBeTrue();
		opts.RecordOpenTelemetryMetrics.ShouldBeTrue();
		opts.RecordCustomMetrics.ShouldBeTrue();
		opts.LogProcessingDetails.ShouldBeTrue();
		opts.SlowOperationThreshold.ShouldBe(TimeSpan.FromSeconds(1));
		opts.BypassMetricsForTypes.ShouldBeNull();
		opts.IncludeMessageSizes.ShouldBeTrue();
		opts.SampleRate.ShouldBe(1.0);
	}

	// --- OutboxOptions ---

	[Fact]
	public void OutboxOptions_HaveDefaults()
	{
		var opts = new OutboxMiddlewareOptions();

		opts.Enabled.ShouldBeFalse();
		opts.DefaultPriority.ShouldBe(0);
		opts.ContinueOnStagingError.ShouldBeFalse();
		opts.BypassOutboxForTypes.ShouldBeNull();
		opts.PublishBatchSize.ShouldBe(100);
		opts.PublishPollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		opts.Retry.MaxRetries.ShouldBe(3);
		opts.Retry.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
		opts.Retry.EnableExponentialRetryBackoff.ShouldBeFalse();
		opts.Retry.MaxRetryDelay.ShouldBe(TimeSpan.FromMinutes(30));
		opts.CleanupAge.ShouldBe(TimeSpan.FromDays(7));
		opts.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
		opts.AdaptivePolling.EnableAdaptivePolling.ShouldBeFalse();
		opts.AdaptivePolling.MinPollingInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
		opts.AdaptivePolling.AdaptivePollingBackoffMultiplier.ShouldBe(2.0);
	}

	// --- OutboxMiddlewareOptionsValidator ---

	[Fact]
	public void OutboxMiddlewareOptionsValidator_AcceptValidDefaults()
	{
		var validator = new OutboxMiddlewareOptionsValidator();
		var opts = new OutboxMiddlewareOptions();

		var result = validator.Validate(null, opts);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void OutboxMiddlewareOptionsValidator_RejectZeroBatchSize()
	{
		var validator = new OutboxMiddlewareOptionsValidator();
		var opts = new OutboxMiddlewareOptions { PublishBatchSize = 0 };

		var result = validator.Validate(null, opts);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("PublishBatchSize");
	}

	[Fact]
	public void OutboxMiddlewareOptionsValidator_RejectZeroPollingInterval()
	{
		var validator = new OutboxMiddlewareOptionsValidator();
		var opts = new OutboxMiddlewareOptions { PublishPollingInterval = TimeSpan.Zero };

		var result = validator.Validate(null, opts);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("PublishPollingInterval");
	}

	[Fact]
	public void OutboxMiddlewareOptionsValidator_RejectNegativeRetries()
	{
		var validator = new OutboxMiddlewareOptionsValidator();
		var opts = new OutboxMiddlewareOptions { Retry = { MaxRetries = -1 } };

		var result = validator.Validate(null, opts);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxRetries");
	}

	[Fact]
	public void OutboxMiddlewareOptionsValidator_RejectMaxRetryDelayLessThanRetryDelay()
	{
		var validator = new OutboxMiddlewareOptionsValidator();
		var opts = new OutboxMiddlewareOptions
		{
			Retry =
			{
				EnableExponentialRetryBackoff = true,
				RetryDelay = TimeSpan.FromMinutes(10),
				MaxRetryDelay = TimeSpan.FromMinutes(5),
			},
		};

		var result = validator.Validate(null, opts);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxRetryDelay");
	}

	[Fact]
	public void OutboxMiddlewareOptionsValidator_RejectMinPollingGreaterThanPublish()
	{
		var validator = new OutboxMiddlewareOptionsValidator();
		var opts = new OutboxMiddlewareOptions
		{
			AdaptivePolling =
			{
				EnableAdaptivePolling = true,
				MinPollingInterval = TimeSpan.FromSeconds(10),
			},
			PublishPollingInterval = TimeSpan.FromSeconds(5),
		};

		var result = validator.Validate(null, opts);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MinPollingInterval");
	}

	[Fact]
	public void OutboxMiddlewareOptionsValidator_RejectLowBackoffMultiplier()
	{
		var validator = new OutboxMiddlewareOptionsValidator();
		var opts = new OutboxMiddlewareOptions
		{
			AdaptivePolling =
			{
				EnableAdaptivePolling = true,
				AdaptivePollingBackoffMultiplier = 1.0,
			},
		};

		var result = validator.Validate(null, opts);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("AdaptivePollingBackoffMultiplier");
	}

	// --- OutboxStagingOptions ---

	[Fact]
	public void OutboxStagingOptions_HaveDefaults()
	{
		var opts = new OutboxStagingOptions();

		opts.Enabled.ShouldBeTrue();
		opts.MaxOutboundMessagesPerOperation.ShouldBe(100);
		opts.CompressMessageData.ShouldBeFalse();
		opts.BypassOutboxForTypes.ShouldBeNull();
	}

	// --- RateLimitingOptions ---

	[Fact]
	public void RateLimitingOptions_HaveDefaults()
	{
		var opts = new RateLimitingOptions();

		opts.Enabled.ShouldBeTrue();
		opts.EnablePerTenantLimiting.ShouldBeTrue();
		opts.DefaultLimit.ShouldNotBeNull();
		opts.DefaultLimit.Algorithm.ShouldBe(MiddlewareRateLimitAlgorithm.TokenBucket);
		opts.DefaultLimit.TokenLimit.ShouldBe(100);
		opts.GlobalLimit.ShouldNotBeNull();
		opts.GlobalLimit.TokenLimit.ShouldBe(1000);
		opts.MessageTypeLimits.ShouldBeEmpty();
		opts.BypassRateLimitingForTypes.ShouldBeNull();
	}

	// --- TenantIdentityOptions ---

	[Fact]
	public void TenantIdentityOptions_HaveDefaults()
	{
		var opts = new TenantIdentityOptions();

		opts.Enabled.ShouldBeTrue();
		opts.ValidateTenantAccess.ShouldBeTrue();
		opts.TenantIdHeader.ShouldBe("X-Tenant-ID");
		opts.TenantNameHeader.ShouldBe("X-Tenant-Name");
		opts.TenantRegionHeader.ShouldBe("X-Tenant-Region");
		opts.DefaultTenantId.ShouldNotBeNull();
		opts.MinTenantIdLength.ShouldBe(1);
		opts.MaxTenantIdLength.ShouldBe(100);
		opts.TenantIdPattern.ShouldBeNull();
	}

	// --- TimeoutOptions (Middleware) ---

	[Fact]
	public void MiddlewareTimeoutOptions_HaveDefaults()
	{
		var opts = new MiddlewareTimeoutOptions();

		opts.Enabled.ShouldBeTrue();
		opts.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		opts.ActionTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		opts.EventTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		opts.DocumentTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		opts.MessageTypeTimeouts.ShouldBeEmpty();
		opts.ThrowOnTimeout.ShouldBeTrue();
	}

	[Fact]
	public void MiddlewareTimeoutOptions_ValidateAcceptsDefaults()
	{
		var opts = new MiddlewareTimeoutOptions();

		Should.NotThrow(() => opts.Validate());
	}

	[Fact]
	public void MiddlewareTimeoutOptions_ValidateRejectsZeroDefaultTimeout()
	{
		var opts = new MiddlewareTimeoutOptions { DefaultTimeout = TimeSpan.Zero };

		Should.Throw<ArgumentException>(() => opts.Validate());
	}

	[Fact]
	public void MiddlewareTimeoutOptions_ValidateRejectsZeroActionTimeout()
	{
		var opts = new MiddlewareTimeoutOptions { ActionTimeout = TimeSpan.Zero };

		Should.Throw<ArgumentException>(() => opts.Validate());
	}

	[Fact]
	public void MiddlewareTimeoutOptions_ValidateRejectsZeroEventTimeout()
	{
		var opts = new MiddlewareTimeoutOptions { EventTimeout = TimeSpan.Zero };

		Should.Throw<ArgumentException>(() => opts.Validate());
	}

	[Fact]
	public void MiddlewareTimeoutOptions_ValidateRejectsZeroDocumentTimeout()
	{
		var opts = new MiddlewareTimeoutOptions { DocumentTimeout = TimeSpan.Zero };

		Should.Throw<ArgumentException>(() => opts.Validate());
	}

	[Fact]
	public void MiddlewareTimeoutOptions_ValidateRejectsZeroMessageTypeTimeout()
	{
		var opts = new MiddlewareTimeoutOptions();
		opts.MessageTypeTimeouts["OrderCommand"] = TimeSpan.Zero;

		Should.Throw<ArgumentException>(() => opts.Validate());
	}

	// --- TransactionOptions ---

	[Fact]
	public void TransactionOptions_HaveDefaults()
	{
		var opts = new MiddlewareTransactionOptions();

		opts.Enabled.ShouldBeTrue();
		opts.RequireTransactionByDefault.ShouldBeTrue();
		opts.EnableDistributedTransactions.ShouldBeFalse();
		opts.DefaultIsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
		opts.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		opts.BypassTransactionForTypes.ShouldBeNull();
	}

	// --- UnifiedBatchingOptions ---

	[Fact]
	public void UnifiedBatchingOptions_HaveDefaults()
	{
		var opts = new UnifiedBatchingOptions();

		opts.MaxBatchSize.ShouldBe(32);
		opts.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(250));
		opts.MaxParallelism.ShouldBe(Environment.ProcessorCount);
		opts.ProcessAsOptimizedBulk.ShouldBeTrue();
		opts.NonBatchableMessageTypes.ShouldBeEmpty();
		opts.BatchFilter.ShouldBeNull();
		opts.BatchKeySelector.ShouldBeNull();
	}

	// --- ValidationOptions ---

	[Fact]
	public void ValidationOptions_HaveDefaults()
	{
		var opts = new ValidationOptions();

		opts.Enabled.ShouldBeTrue();
		opts.UseDataAnnotations.ShouldBeTrue();
		opts.UseCustomValidation.ShouldBeTrue();
		opts.StopOnFirstError.ShouldBeFalse();
		opts.BypassValidationForTypes.ShouldBeNull();
	}

	// --- VersionCheckHeaders ---

	[Fact]
	public void VersionCheckHeaders_HaveDefaults()
	{
		var headers = new VersionCheckHeaders();

		headers.VersionHeaderName.ShouldBe("X-Message-Version");
		headers.SchemaIdHeaderName.ShouldBe("X-Schema-MessageId");
		headers.ProducerVersionHeaderName.ShouldBe("X-Producer-Version");
		headers.ProducerServiceHeaderName.ShouldBe("X-Producer-Service");
	}
}
