// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Telemetry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.TestFakes;

using Xunit;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Author≠implementer RED-first regression lock (bead <c>wjp8nb</c>): <see cref="RetryMiddleware"/>'s
/// non-retryable classification MUST walk the type hierarchy (assignable-from), so an exception that
/// DERIVES from a configured non-retryable type is itself treated as non-retryable and is NOT retried.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect:</b> <c>IsExceptionRetryable</c> tests
/// <c>NonRetryableExceptions.Contains(exception.GetType())</c> — an EXACT-type <see cref="HashSet{T}"/>
/// match. <see cref="TenantIsolationViolationException"/> derives from
/// <see cref="InvalidOperationException"/>; with <see cref="InvalidOperationException"/> configured
/// non-retryable, the exact-type <c>Contains</c> misses the derived type, so the classification falls
/// through to the failure classifier — which maps an unrecognised exception to <c>Transient</c> — and the
/// tenancy-isolation violation is RETRIED. Retrying a tenant-isolation violation multiplies the isolation
/// exposure, so this is a security defect, not merely a wasted retry.
/// </para>
/// <para>
/// <b>Property, not mechanism</b> (testing-patterns §3 corollary): the arms assert the observable retry
/// count, so they hold under any fix that makes the non-retryable check assignable-from (walk the
/// hierarchy) rather than exact-type.
/// </para>
/// <para>
/// <b>Two dimensions, each safety+liveness (testing-patterns §3):</b>
/// (1) HIERARCHY — a DERIVED non-retryable is not retried (RED against the exact-type impl, GREEN on
/// assignable-from), while a genuinely-transient exception unrelated to the non-retryable set is STILL
/// retried (the fix cannot satisfy safety by broadening to "never retry").
/// (2) PRECEDENCE — the non-retryable floor is checked BEFORE the <c>RetryableExceptions</c> allowlist, so a
/// derived non-retryable is not retried EVEN WHEN explicitly allow-listed (RED against the allowlist-first
/// ordering, GREEN on floor-first), while an allow-listed transient not under the floor is STILL retried
/// (the floor-first ordering must not disable the allowlist).
/// </para>
/// </remarks>
[Collection("Performance Tests")]
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed class RetryMiddlewareNonRetryableHierarchyShould
{
	private const int MaxAttempts = 3;

	private readonly ILogger<RetryMiddleware> _logger =
		NullLoggerFactory.Instance.CreateLogger<RetryMiddleware>();

	private RetryMiddleware CreateMiddleware(RetryOptions options) =>
		new(Microsoft.Extensions.Options.Options.Create(options), NullTelemetrySanitizer.Instance, _logger);

	// A configured non-retryable BASE, with the RetryableExceptions allow-list left empty so the
	// NonRetryableExceptions path is the one under test (a non-empty allow-list short-circuits it).
	private static RetryOptions OptionsWithInvalidOperationNonRetryable()
	{
		var options = new RetryOptions
		{
			MaxAttempts = MaxAttempts,
			BaseDelay = TimeSpan.FromMilliseconds(1),
			BackoffStrategy = BackoffStrategy.Fixed,
		};
		// Explicit so the lock does not depend on the default-set contents (F5 hygiene).
		_ = options.NonRetryableExceptions.Add(typeof(InvalidOperationException));
		return options;
	}

	private async Task<int> CountAttemptsAsync(RetryOptions options, Exception toThrow)
	{
		var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var attemptCount = 0;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			attemptCount++;
			throw toThrow;
		}

		try
		{
			// The middleware may rethrow on a non-retryable exception, or return a failed result after
			// exhausting retries — the observable that distinguishes the two behaviours is the attempt count,
			// not the return path, so either outcome is tolerated here.
			_ = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None)
				.ConfigureAwait(false);
		}
		catch (Exception)
		{
			// swallowed — see above; the assertion is on attemptCount.
		}

		return attemptCount;
	}

	[Fact]
	public async Task NotRetry_ADerivedTypeOfAConfiguredNonRetryableException()
	{
		// SAFETY. RED against committed HEAD: the exact-type Contains misses TenantIsolationViolationException
		// (derived from the configured non-retryable InvalidOperationException), so it falls through to the
		// classifier (unrecognised => Transient) and IS retried up to MaxAttempts.
		var attempts = await CountAttemptsAsync(
			OptionsWithInvalidOperationNonRetryable(),
			new TenantIsolationViolationException()).ConfigureAwait(false);

		attempts.ShouldBe(
			1,
			"EXPECTED RED until the non-retryable check is assignable-from (tracked: wjp8nb). "
			+ "TenantIsolationViolationException derives from InvalidOperationException (configured "
			+ "non-retryable), so it must be treated as non-retryable and invoked exactly once — retrying a "
			+ "tenant-isolation violation multiplies the isolation exposure");
	}

	[Fact]
	public async Task StillRetry_AGenuinelyTransientExceptionUnrelatedToTheNonRetryableSet()
	{
		// LIVENESS. GREEN now and after the fix. A transient exception that does NOT derive from the
		// configured non-retryable base must still be retried — the assignable-from fix must not over-broaden
		// into "never retry". Fails if the fix suppresses all retries.
		var attempts = await CountAttemptsAsync(
			OptionsWithInvalidOperationNonRetryable(),
			new TimeoutException("transient")).ConfigureAwait(false);

		attempts.ShouldBe(
			MaxAttempts,
			"a transient exception unrelated to the non-retryable set is still retried to the attempt cap — "
			+ "the derived-type exclusion must not broaden into suppressing legitimate retries");
	}

	[Fact]
	public async Task NotRetry_ADerivedNonRetryable_EvenWhenItIsAlsoInTheRetryableAllowlist()
	{
		// PRECEDENCE — SAFETY. RED against committed HEAD: the RetryableExceptions allowlist is checked BEFORE
		// the non-retryable floor, so an allowlist entry re-enables retrying the derived non-retryable. GREEN
		// once the floor is checked first. This is the ONLY config that actually leaked pre-reorder — the
		// default/empty-allowlist path is covered by the hierarchy arms above; explicit allow-listing is the
		// bypass. Binds "the non-retryable floor takes precedence over the allowlist".
		var options = OptionsWithInvalidOperationNonRetryable();
		// An allowlist that WOULD retry the violation if it were consulted before the floor.
		_ = options.RetryableExceptions.Add(typeof(TenantIsolationViolationException));

		var attempts = await CountAttemptsAsync(options, new TenantIsolationViolationException())
			.ConfigureAwait(false);

		attempts.ShouldBe(
			1,
			"EXPECTED RED until the non-retryable floor is checked before the RetryableExceptions allowlist "
			+ "(tracked: wjp8nb). TenantIsolationViolationException derives from the configured non-retryable "
			+ "InvalidOperationException, so it must NOT be retried even when explicitly allow-listed — the "
			+ "floor takes precedence, or an allowlist config re-opens the cross-tenant isolation exposure");
	}

	[Fact]
	public async Task StillRetry_AnAllowlistedTransient_WhenTheFloorIsCheckedFirst()
	{
		// PRECEDENCE — LIVENESS. The floor-first ordering must not disable the allowlist: a transient that is
		// allow-listed and NOT under the non-retryable floor is still retried. Fails if checking the floor
		// first accidentally short-circuits the allowlist path.
		var options = OptionsWithInvalidOperationNonRetryable();
		_ = options.RetryableExceptions.Add(typeof(TimeoutException));

		var attempts = await CountAttemptsAsync(options, new TimeoutException("transient"))
			.ConfigureAwait(false);

		attempts.ShouldBe(
			MaxAttempts,
			"an allow-listed transient unrelated to the non-retryable floor is still retried — checking the "
			+ "floor first must not suppress the allowlist path");
	}
}
