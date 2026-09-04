// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Reflection;
using System.Threading.RateLimiting;

using Excalibur.Dispatch;
using Excalibur.Security;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.RateLimiting;

/// <summary>
/// Guards the lifetime of the per-key rate limiters. The rate-limit key is attacker-controlled
/// (client IP, hashed API key), so a caller that rotates identifiers must not be able to grow the
/// limiter set without bound; and a key that is actively being limited must keep its limiter and
/// its spent budget rather than being reclaimed and handed a fresh one.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class RateLimitingMiddlewareLimiterLifetimeShould
{
	// The framework limiter reclaims a partition after it has been idle for 10 seconds; allow margin.
	private static readonly TimeSpan ReclaimWindow = TimeSpan.FromSeconds(20);

	private static readonly IMessageResult Allowed = A.Fake<IMessageResult>();

	private static readonly DispatchRequestDelegate Next =
		(msg, ctx, ct) => new ValueTask<IMessageResult>(Allowed);

	[Fact]
	public async Task NotAccumulatePerKeyStateOfItsOwnUnderAFloodOfDistinctKeys()
	{
		const int DistinctKeys = 5_000;

		var options = Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.TokenBucket,
			DefaultLimits = new RateLimits
			{
				TokenLimit = 1,
				TokensPerPeriod = 1,
				ReplenishmentPeriodSeconds = 600,
				QueueLimit = 0,
			},
		});

		await using var sut = new RateLimitingMiddleware(options, NullLogger<RateLimitingMiddleware>.Instance);

		var message = Message();
		var context = new TestContext();

		// Liveness arm: every distinct key really does get its own budget, so the middleware under the
		// safety arm below is doing the per-key limiting whose state the safety arm bounds.
		for (var i = 0; i < DistinctKeys; i++)
		{
			context.Items["ClientIp"] = i.ToString(System.Globalization.CultureInfo.InvariantCulture);

			(await sut.InvokeAsync(message, context, Next, CancellationToken.None))
				.ShouldNotBeOfType<RateLimitExceededResult>($"key {i} should start with its own budget");

			(await sut.InvokeAsync(message, context, Next, CancellationToken.None))
				.ShouldBeOfType<RateLimitExceededResult>($"key {i} should be limited after spending its budget");
		}

		// Safety arm: the middleware keeps no per-key collection of its own. The rate-limit key is
		// attacker-controlled (client IP, hashed API key), so a map the middleware owns and never
		// bounds is a memory-exhaustion vector; partition lifetime belongs to the framework limiter,
		// which releases a partition once its key falls idle.
		var fields = typeof(RateLimitingMiddleware)
			.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			.Where(f => typeof(System.Collections.IEnumerable).IsAssignableFrom(f.FieldType))
			.Select(f => $"{f.FieldType.Name} {f.Name}")
			.ToList();

		fields.ShouldBeEmpty(
			$"the middleware holds a collection keyed by attacker-controlled input: {string.Join(", ", fields)}");

		typeof(RateLimitingMiddleware)
			.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
			.Select(f => f.FieldType)
			.ShouldContain(typeof(PartitionedRateLimiter<string>));
	}

	[Fact]
	public async Task KeepTheLimiterAndTheSpentBudgetOfAnActivelyLimitedKey()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.TokenBucket,
			DefaultLimits = new RateLimits
			{
				TokenLimit = 1,
				TokensPerPeriod = 1,
				// Long enough that nothing observed below can be explained by replenishment.
				ReplenishmentPeriodSeconds = 600,
				QueueLimit = 0,
			},
		});

		await using var sut = new RateLimitingMiddleware(options, NullLogger<RateLimitingMiddleware>.Instance);

		var context = ContextForIp("203.0.113.7");

		(await sut.InvokeAsync(Message(), context, Next, CancellationToken.None))
			.ShouldNotBeOfType<RateLimitExceededResult>();

		// Spend the budget, then keep the key active for longer than the reclaim window.
		var clock = Stopwatch.StartNew();
		do
		{
			(await sut.InvokeAsync(Message(), context, Next, CancellationToken.None))
				.ShouldBeOfType<RateLimitExceededResult>(
					$"the key regained budget after {clock.Elapsed.TotalSeconds:F0}s without any replenishment");

			await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
		}
		while (clock.Elapsed < ReclaimWindow);
	}

	private static IDispatchMessage Message() => new ProbeMessage();

	private static TestContext ContextForIp(string ip) => new() { Items = { ["ClientIp"] = ip } };

	private sealed class ProbeMessage : IDispatchMessage;

	/// <summary>
	/// Hand-written so the flood measurement sees only the state the middleware itself retains - a
	/// mocking framework records every call it receives, which would dominate the reading.
	/// </summary>
	private sealed class TestContext : IMessageContext
	{
		public string? MessageId { get; set; }

		public string? CorrelationId { get; set; }

		public string? CausationId { get; set; }

		public object? Result { get; set; }

		public IDispatchMessage? Message { get; set; }

		public IServiceProvider RequestServices { get; set; } = null!;

		public IDictionary<string, object> Items { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

		public IDictionary<Type, object> Features { get; } = new Dictionary<Type, object>();
	}
}
