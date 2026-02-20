// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchResilienceOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ElasticsearchResilienceOptions();

		sut.Enabled.ShouldBeTrue();
		sut.Retry.ShouldNotBeNull();
		sut.CircuitBreaker.ShouldNotBeNull();
		sut.Timeouts.ShouldNotBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var retry = new RetryPolicyOptions { MaxAttempts = 5 };
		var cb = new CircuitBreakerOptions { FailureThreshold = 10 };
		var timeouts = new TimeoutOptions { SearchTimeout = TimeSpan.FromSeconds(60) };

		var sut = new ElasticsearchResilienceOptions
		{
			Enabled = false,
			Retry = retry,
			CircuitBreaker = cb,
			Timeouts = timeouts,
		};

		sut.Enabled.ShouldBeFalse();
		sut.Retry.ShouldBeSameAs(retry);
		sut.CircuitBreaker.ShouldBeSameAs(cb);
		sut.Timeouts.ShouldBeSameAs(timeouts);
	}
}
