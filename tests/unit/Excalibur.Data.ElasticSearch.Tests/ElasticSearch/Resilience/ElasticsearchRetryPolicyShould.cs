// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net.Sockets;

using Elastic.Transport;

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Resilience;

namespace Excalibur.Data.Tests.ElasticSearch.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchRetryPolicyShould
{
	private readonly ElasticsearchRetryPolicy _sut;

	public ElasticsearchRetryPolicyShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				Retry = new RetryPolicyOptions
				{
					Enabled = true,
					MaxAttempts = 3,
					BaseDelay = TimeSpan.FromSeconds(1),
					MaxDelay = TimeSpan.FromSeconds(30),
					JitterFactor = 0.1,
					UseExponentialBackoff = true,
				},
			},
		});

		_sut = new ElasticsearchRetryPolicy(options);
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new ElasticsearchRetryPolicy(null!));
	}

	[Fact]
	public void ReturnMaxAttempts()
	{
		_sut.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void ThrowWhenAttemptNumberIsNegative()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => _sut.GetRetryDelay(-1));
	}

	[Fact]
	public void ReturnZeroDelayWhenAttemptsExceeded()
	{
		var delay = _sut.GetRetryDelay(10);
		delay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ReturnPositiveDelayForValidAttempt()
	{
		var delay = _sut.GetRetryDelay(0);
		delay.TotalMilliseconds.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void UseExponentialBackoff()
	{
		// With exponential backoff, delay should approximately double
		// Base = 1000ms, so attempt 0 ~= 1000ms, attempt 1 ~= 2000ms (with jitter)
		var delay0 = _sut.GetRetryDelay(0);
		var delay1 = _sut.GetRetryDelay(1);

		// delay1 should be roughly 2x delay0 (with jitter)
		delay1.TotalMilliseconds.ShouldBeGreaterThan(delay0.TotalMilliseconds * 1.5);
	}

	[Fact]
	public void CapDelayAtMaxDelay()
	{
		// Very high attempt number should cap at max delay + jitter
		var delay = _sut.GetRetryDelay(2);
		// With jitter, max should be around max delay * (1 + jitterFactor)
		delay.TotalSeconds.ShouldBeLessThanOrEqualTo(33); // 30 * 1.1
	}

	[Fact]
	public void ReturnFixedDelayWhenExponentialBackoffDisabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				Retry = new RetryPolicyOptions
				{
					Enabled = true,
					MaxAttempts = 3,
					BaseDelay = TimeSpan.FromSeconds(2),
					UseExponentialBackoff = false,
					JitterFactor = 0,
				},
			},
		});

		var sut = new ElasticsearchRetryPolicy(options);

		// Act
		var delay0 = sut.GetRetryDelay(0);
		var delay1 = sut.GetRetryDelay(1);

		// Assert — both should be the same base delay
		delay0.ShouldBe(TimeSpan.FromSeconds(2));
		delay1.ShouldBe(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void ReturnZeroDelayWhenDisabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				Retry = new RetryPolicyOptions { Enabled = false },
			},
		});

		var sut = new ElasticsearchRetryPolicy(options);

		// Act
		var delay = sut.GetRetryDelay(0);

		// Assert
		delay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ShouldRetryForTimeoutException()
	{
		_sut.ShouldRetry(new TimeoutException(), 1).ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetryForHttpRequestException()
	{
		_sut.ShouldRetry(new HttpRequestException(), 1).ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetryForSocketException()
	{
		_sut.ShouldRetry(new SocketException(), 1).ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetryForIOException()
	{
		_sut.ShouldRetry(new IOException(), 1).ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetryForTaskCanceledWithoutCancellation()
	{
		_sut.ShouldRetry(new TaskCanceledException(), 1).ShouldBeTrue();
	}

	[Fact]
	public void ShouldNotRetryForGenericException()
	{
		_sut.ShouldRetry(new InvalidOperationException(), 1).ShouldBeFalse();
	}

	[Fact]
	public void ShouldNotRetryWhenDisabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				Retry = new RetryPolicyOptions { Enabled = false },
			},
		});

		var sut = new ElasticsearchRetryPolicy(options);

		// Act & Assert
		sut.ShouldRetry(new TimeoutException(), 1).ShouldBeFalse();
	}

	[Fact]
	public void ShouldNotRetryWhenMaxAttemptsExceeded()
	{
		// MaxAttempts = 3, so attempt 5 (1-based) should not retry
		_sut.ShouldRetry(new TimeoutException(), 5).ShouldBeFalse();
	}

	[Fact]
	public void ApplyJitterToDelay()
	{
		// Run multiple times and ensure not all delays are identical
		var delays = new HashSet<double>();
		for (var i = 0; i < 10; i++)
		{
			delays.Add(_sut.GetRetryDelay(0).TotalMilliseconds);
		}

		// With jitter, we should get some variation (at least 2 different values)
		delays.Count.ShouldBeGreaterThan(1);
	}

	[Fact]
	public void ReturnZeroJitterWhenJitterFactorIsZero()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				Retry = new RetryPolicyOptions
				{
					Enabled = true,
					MaxAttempts = 3,
					BaseDelay = TimeSpan.FromSeconds(1),
					JitterFactor = 0,
					UseExponentialBackoff = false,
				},
			},
		});

		var sut = new ElasticsearchRetryPolicy(options);

		// Act — all should return the exact base delay
		var delay1 = sut.GetRetryDelay(0);
		var delay2 = sut.GetRetryDelay(0);

		// Assert
		delay1.ShouldBe(delay2);
		delay1.ShouldBe(TimeSpan.FromSeconds(1));
	}
}
