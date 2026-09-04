// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Resilience;

using Microsoft.Extensions.Options;

using Polly.CircuitBreaker;

namespace Excalibur.Data.Tests.ElasticSearch.Resilience;

/// <summary>
/// Locks the retry and circuit-breaker behaviour every Elasticsearch call runs through.
/// </summary>
/// <remarks>
/// The retry ladder and breaker state machine used to be written here by hand. They are now a Polly
/// pipeline, and what matters is that the observable behaviour did not move: transient failures are
/// retried, non-transient ones are not, and a run of failures stops the calls.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchResiliencePipelineShould
{
	private static ElasticsearchResiliencePipeline Build(
		int maxAttempts = 3,
		bool breakerEnabled = false,
		int minimumThroughput = 2) =>
		new(Options.Create(new ElasticsearchResilienceOptions
		{
			Retry = new ElasticSearchRetryPolicyOptions
			{
				Enabled = true,
				MaxAttempts = maxAttempts,
				BaseDelay = TimeSpan.Zero,
				MaxDelay = TimeSpan.Zero,
				UseExponentialBackoff = false,
				JitterFactor = 0,
			},
			CircuitBreaker = new CircuitBreakerOptions
			{
				Enabled = breakerEnabled,
				MinimumThroughput = minimumThroughput,
				FailureRateThreshold = 0.1,
				SamplingDuration = TimeSpan.FromSeconds(30),
				BreakDuration = TimeSpan.FromSeconds(30),
			},
		}));

	[Fact]
	public async Task RetryATransientFailureUpToTheConfiguredAttempts()
	{
		// SAFETY plus arithmetic: Polly counts retries where the option counts attempts, so an
		// off-by-one here would silently give every consumer an extra call to Elasticsearch.
		var pipeline = Build(maxAttempts: 3);
		var calls = 0;

		var thrown = await Should.ThrowAsync<TimeoutException>(async () =>
			await pipeline.ExecuteAsync<int>(
				_ =>
				{
					calls++;
					throw new TimeoutException("transient");
				},
				TestContext.Current.CancellationToken));

		calls.ShouldBe(3, "three attempts means the initial call plus two retries");
		thrown.Message.ShouldBe("transient");
	}

	[Fact]
	public async Task NotRetryAFailureThatIsNotTransient()
	{
		// LIVENESS for the predicate: retrying everything would satisfy the arm above and turn a
		// flat rejection into three of them.
		var pipeline = Build(maxAttempts: 3);
		var calls = 0;

		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await pipeline.ExecuteAsync<int>(
				_ =>
				{
					calls++;
					throw new InvalidOperationException("not transient");
				},
				TestContext.Current.CancellationToken));

		calls.ShouldBe(1, "a non-transient failure is returned to the caller, not retried");
	}

	[Fact]
	public async Task ReturnTheValueWithoutRetryingWhenTheCallSucceeds()
	{
		// The arm that fails if the pipeline ever retries a success.
		var pipeline = Build();
		var calls = 0;

		var result = await pipeline.ExecuteAsync(
			_ =>
			{
				calls++;
				return ValueTask.FromResult(42);
			},
			TestContext.Current.CancellationToken);

		result.ShouldBe(42);
		calls.ShouldBe(1);
	}

	[Fact]
	public async Task StopCallingOnceEnoughFailuresHaveAccumulated()
	{
		// SAFETY: the breaker exists to stop hammering a failing cluster. Without it the calls
		// continue indefinitely, which is the behaviour it was written to prevent.
		var pipeline = Build(maxAttempts: 1, breakerEnabled: true, minimumThroughput: 2);

		for (var i = 0; i < 6; i++)
		{
			try
			{
				_ = await pipeline.ExecuteAsync<int>(
					_ => throw new TimeoutException("transient"),
					TestContext.Current.CancellationToken);
			}
			catch (TimeoutException)
			{
				// expected while the circuit is still closed
			}
			catch (BrokenCircuitException)
			{
				return; // the circuit opened, which is the point of the test
			}
		}

		Assert.Fail("the circuit never opened despite a sustained run of transient failures");
	}

	[Fact]
	public void TreatTransportAndNetworkFailuresAsTransient()
	{
		ElasticsearchResiliencePipeline.IsTransient(new HttpRequestException("net")).ShouldBeTrue();
		ElasticsearchResiliencePipeline.IsTransient(new TimeoutException()).ShouldBeTrue();
		ElasticsearchResiliencePipeline.IsTransient(new TaskCanceledException()).ShouldBeTrue();

		// And the other half of the predicate: an ordinary failure is not a reason to try again.
		ElasticsearchResiliencePipeline.IsTransient(new InvalidOperationException()).ShouldBeFalse();
		ElasticsearchResiliencePipeline.IsTransient(new ArgumentException()).ShouldBeFalse();
	}

	[Fact]
	public async Task OpenOnFailuresThatAreNotWorthRetrying()
	{
		// The breaker's predicate is deliberately wider than the retry's. A connection refusal
		// surfaces as a TransportException carrying no HTTP status, so the retry test says "not
		// transient" -- and when both shared one predicate that failure never reached the circuit,
		// leaving the breaker unable to open on the most basic failure there is.
		var pipeline = Build(maxAttempts: 1, breakerEnabled: true, minimumThroughput: 2);

		for (var i = 0; i < 6; i++)
		{
			try
			{
				_ = await pipeline.ExecuteAsync<int>(
					_ => throw new InvalidOperationException("connection refused"),
					TestContext.Current.CancellationToken);
			}
			catch (InvalidOperationException)
			{
				// still closed
			}
			catch (BrokenCircuitException)
			{
				return; // opened on a failure the retry ladder would not have repeated
			}
		}

		Assert.Fail("the circuit never opened on a non-transient failure");
	}

	[Fact]
	public async Task StillNotRetryAFailureThatIsNotTransient()
	{
		// The other half of that split: widening the BREAKER must not widen the RETRY, or a flat
		// rejection turns into three of them.
		var pipeline = Build(maxAttempts: 3, breakerEnabled: false);
		var calls = 0;

		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await pipeline.ExecuteAsync<int>(
				_ =>
				{
					calls++;
					throw new InvalidOperationException("not transient");
				},
				TestContext.Current.CancellationToken));

		calls.ShouldBe(1);
	}

	[Theory]
	[InlineData(429)]
	[InlineData(502)]
	[InlineData(503)]
	[InlineData(504)]
	public void TreatAnInvalidResponseCarryingARetriableStatusCodeAsTransient(int statusCode)
	{
		ElasticsearchResiliencePipeline
			.IsTransient(new ElasticsearchInvalidResponseException("search", statusCode))
			.ShouldBeTrue();
	}

	[Theory]
	[InlineData(404)]
	[InlineData(400)]
	[InlineData(500)]
	[InlineData(null)]
	public void NotTreatAnInvalidResponseCarryingANonRetriableStatusCodeAsTransient(int? statusCode)
	{
		ElasticsearchResiliencePipeline
			.IsTransient(new ElasticsearchInvalidResponseException("search", statusCode))
			.ShouldBeFalse();
	}

	[Theory]
	[InlineData(429, true)]
	[InlineData(502, true)]
	[InlineData(503, true)]
	[InlineData(504, true)]
	[InlineData(404, false)]
	[InlineData(400, false)]
	[InlineData(500, false)]
	[InlineData(null, false)]
	public void JudgeATransportExceptionByTheSameStatusCodeRuleAsAnInvalidResponse(int? statusCode, bool expectedTransient)
	{
		// The two branches share IsRetriableStatusCode -- this proves a TransportException and an
		// ElasticsearchInvalidResponseException agree on every status code, so they cannot drift apart.
		ElasticsearchResiliencePipeline.IsTransient(CreateTransportException(statusCode)).ShouldBe(expectedTransient);
	}

	[Fact]
	public void NotTreatAPlainInvalidOperationExceptionAsTransient()
	{
		// Load-bearing: ElasticsearchInvalidResponseException DERIVES from InvalidOperationException.
		// A blanket InvalidOperationException => true would also make the unrelated "circuit breaker is
		// open" signal retriable -- the exact widening IsTransient's own remarks say was deliberately
		// refused. This test must fail if that widening is ever reintroduced.
		ElasticsearchResiliencePipeline
			.IsTransient(new InvalidOperationException("Circuit breaker is open for Search operations"))
			.ShouldBeFalse();
	}

	/// <summary>
	/// Constructs a real <see cref="TransportException" /> carrying the given HTTP status code via its
	/// <see cref="Elastic.Transport.ApiCallDetails" />.
	/// </summary>
	/// <remarks>
	/// The SDK deliberately keeps both the exception's <c>ApiCallDetails</c> setter and
	/// <see cref="Elastic.Transport.ApiCallDetails" />'s own constructor/<c>HttpStatusCode</c> setter
	/// internal to <c>Elastic.Transport</c> -- there is no public path to manufacture one carrying a
	/// specific status code, and no <c>InternalsVisibleTo</c> grant reaches this test assembly. Reflection
	/// is the only way to exercise <see cref="ElasticsearchResiliencePipeline.IsTransient" /> against a
	/// real SDK exception rather than a hand-rolled stand-in; it constructs the INPUT only, the assertion
	/// still runs the real production predicate.
	/// </remarks>
	private static TransportException CreateTransportException(int? statusCode)
	{
		var detailsType = typeof(ApiCallDetails);
		var detailsCtor = detailsType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)
			?? throw new InvalidOperationException("Elastic.Transport.ApiCallDetails constructor shape changed.");
		var details = (ApiCallDetails)detailsCtor.Invoke(null);

		var statusCodeProperty = detailsType.GetProperty(nameof(ApiCallDetails.HttpStatusCode))
			?? throw new InvalidOperationException("Elastic.Transport.ApiCallDetails.HttpStatusCode was removed or renamed.");
		statusCodeProperty.SetValue(details, statusCode);

		var exception = new TransportException("simulated transport failure");
		var apiCallDetailsProperty = typeof(TransportException).GetProperty(nameof(TransportException.ApiCallDetails))
			?? throw new InvalidOperationException("Elastic.Transport.TransportException.ApiCallDetails was removed or renamed.");
		apiCallDetailsProperty.SetValue(exception, details);

		return exception;
	}
}
