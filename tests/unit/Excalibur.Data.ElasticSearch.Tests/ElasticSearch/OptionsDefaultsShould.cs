// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class OptionsDefaultsShould
{
	[Fact]
	public void CircuitBreakerOptionsHaveCorrectDefaults()
	{
		var options = new CircuitBreakerOptions();

		options.Enabled.ShouldBeTrue();
		options.FailureThreshold.ShouldBe(5);
		options.MinimumThroughput.ShouldBe(10);
		options.BreakDuration.ShouldBe(TimeSpan.FromSeconds(30));
		options.SamplingDuration.ShouldBe(TimeSpan.FromSeconds(60));
		options.FailureRateThreshold.ShouldBe(0.5);
	}

	[Fact]
	public void RetryPolicyOptionsHaveCorrectDefaults()
	{
		var options = new RetryPolicyOptions();

		options.Enabled.ShouldBeTrue();
		options.MaxAttempts.ShouldBe(3);
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
		options.JitterFactor.ShouldBe(0.1);
		options.UseExponentialBackoff.ShouldBeTrue();
	}

	[Fact]
	public void TimeoutOptionsHaveCorrectDefaults()
	{
		var options = new TimeoutOptions();

		options.SearchTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.IndexTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		options.BulkTimeout.ShouldBe(TimeSpan.FromSeconds(120));
		options.DeleteTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void ElasticsearchResilienceOptionsHaveCorrectDefaults()
	{
		var options = new ElasticsearchResilienceOptions();

		options.Enabled.ShouldBeTrue();
		options.Retry.ShouldNotBeNull();
		options.CircuitBreaker.ShouldNotBeNull();
		options.Timeouts.ShouldNotBeNull();
	}

	[Fact]
	public void ProjectionOptionsHaveCorrectDefaults()
	{
		var options = new ProjectionOptions();

		options.IndexPrefix.ShouldBe("projections");
		options.ErrorHandling.ShouldNotBeNull();
		options.RetryPolicy.ShouldNotBeNull();
		options.ConsistencyTracking.ShouldNotBeNull();
		options.SchemaEvolution.ShouldNotBeNull();
		options.RebuildManager.ShouldNotBeNull();
	}

	[Fact]
	public void ProjectionRetryOptionsHaveCorrectDefaults()
	{
		var options = new ProjectionRetryOptions();

		options.Enabled.ShouldBeTrue();
		options.MaxIndexAttempts.ShouldBe(3);
		options.MaxBulkAttempts.ShouldBe(2);
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
		options.UseExponentialBackoff.ShouldBeTrue();
		options.JitterFactor.ShouldBe(0.2);
	}

	[Fact]
	public void ConsistencyTrackingOptionsHaveCorrectDefaults()
	{
		var options = new ConsistencyTrackingOptions();

		options.Enabled.ShouldBeTrue();
		options.ExpectedMaxLag.ShouldBe(TimeSpan.FromSeconds(5));
		options.SLAPercentage.ShouldBe(99.0);
		options.MetricsInterval.ShouldBe(TimeSpan.FromMinutes(1));
		options.EnableAlerting.ShouldBeTrue();
	}

	[Fact]
	public void RebuildManagerOptionsHaveCorrectDefaults()
	{
		var options = new RebuildManagerOptions();

		options.Enabled.ShouldBeTrue();
		options.DefaultBatchSize.ShouldBe(1000);
		options.MaxDegreeOfParallelism.ShouldBe(4);
		options.UseAliasing.ShouldBeTrue();
		options.OperationTimeout.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void ConnectionPoolTypeHasExpectedValues()
	{
		// Verify enum values exist
		Enum.IsDefined(ConnectionPoolType.Static).ShouldBeTrue();
		Enum.IsDefined(ConnectionPoolType.Sniffing).ShouldBeTrue();
	}

	[Fact]
	public void ElasticsearchDeadLetterOptionsHaveCorrectDefaults()
	{
		var options = new ElasticsearchDeadLetterOptions();

		options.DeadLetterIndexPrefix.ShouldBe("dead-letters");
		options.MaxRetryCount.ShouldBe(3);
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}
}
