// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Rebuilds the general functional coverage for <see cref="InMemoryCacheTagTracker"/> that was lost
/// when the tracker moved from a key-set contract to per-tag version stamps.
/// </summary>
/// <remarks>
/// <para>
/// The conformance kit (<c>CacheTagTrackerConformanceTestKit</c>) already binds the contract every
/// implementation shares. These arms cover the ground the kit deliberately does not reach: argument
/// validation, the shape of a stamp, cancellation, and the concurrency claim this implementation makes
/// about itself in a source comment — that a race through <c>GetOrAdd</c> may run the factory more
/// than once but can never let two callers disagree about a tag's current stamp.
/// </para>
/// <para>
/// That concurrency arm is the load-bearing one. A disagreement there would not throw or fail
/// visibly; it would silently invalidate one caller's cache entries because the two saw different
/// stamps for a tag nobody bumped.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCacheTagTrackerShould
{
	private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

	// ---- Argument validation (failure paths). ----

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task RejectAnAbsentTagOnResolve(string? tag)
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await tracker.GetOrCreateStampAsync(tag!, Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task RejectAnAbsentTagOnBump(string? tag)
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await tracker.BumpStampAsync(tag!, Ct));
	}

	/// <summary>
	/// Whitespace is a legitimate — if odd — tag name, not an absent one. Pinned so a future tightening
	/// of the guard to <c>ThrowIfNullOrWhiteSpace</c> is a deliberate contract change rather than an
	/// accident that starts throwing on data a consumer already caches under.
	/// </summary>
	[Fact]
	public async Task TreatAWhitespaceTagAsARealTag()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		var stamp = await tracker.GetOrCreateStampAsync(" ", Ct);

		stamp.ShouldNotBeNullOrEmpty();
	}

	// ---- Stamp shape. ----

	[Fact]
	public async Task ProduceAnOpaqueStampThatIsSafeInACacheKeyAndCarriesNoOrdering()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		var stamp = await tracker.GetOrCreateStampAsync("orders", Ct);

		stamp.ShouldNotBeNullOrWhiteSpace();
		stamp.ShouldNotContain(":", Case.Sensitive,
			"stamps are concatenated into cache keys built around ':' separators");
		stamp.ToCharArray().ShouldAllBe(
			c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_',
			"a stamp is base64url-encoded, so it must never need escaping in a key or a backend");
	}

	[Fact]
	public async Task ProduceUnpredictableStamps_SoOneCannotBeGuessedToFalselyCompareAsCurrent()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		var stamps = new HashSet<string>(StringComparer.Ordinal);
		for (var i = 0; i < 200; i++)
		{
			_ = stamps.Add(await tracker.GetOrCreateStampAsync($"tag-{i}", Ct));
		}

		stamps.Count.ShouldBe(200,
			"stamps are drawn from a CSPRNG; a collision across 200 draws would mean the stamp is "
			+ "derived from something predictable, letting a stale entry compare as current");
	}

	// ---- Concurrency: the claim the implementation makes about itself. ----

	[Fact]
	public async Task NeverLetConcurrentCallersDisagreeAboutATagsStamp()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = new InMemoryCacheTagTracker(meterFactory);
		using var barrier = new Barrier(participantCount: 32);

		// Every task blocks on the barrier so the resolutions collide on a COLD tag, which is the only
		// window in which GetOrAdd's factory can run more than once.
		var tasks = Enumerable.Range(0, 32).Select(_unused => Task.Run(
			async () =>
			{
				_ = barrier.SignalAndWait(TimeSpan.FromSeconds(10));
				return await tracker.GetOrCreateStampAsync("contended", Ct);
			},
			Ct));

		var observed = await Task.WhenAll(tasks);

		observed.Distinct(StringComparer.Ordinal).Count().ShouldBe(1,
			"a race may discard a redundantly created stamp, but every caller must receive the one "
			+ "value that was actually stored — two callers disagreeing would silently invalidate one "
			+ "of their cache entries for a tag nobody bumped");
	}

	[Fact]
	public async Task RemainConsistentUnderConcurrentResolvesAndBumps()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		var readers = Enumerable.Range(0, 16).Select(_ => Task.Run(
			async () =>
			{
				for (var i = 0; i < 50; i++)
				{
					var stamp = await tracker.GetOrCreateStampAsync("churned", Ct);
					stamp.ShouldNotBeNullOrEmpty("a resolve must never observe a torn or absent stamp mid-bump");
				}
			},
			Ct));

		var bumpers = Enumerable.Range(0, 4).Select(_ => Task.Run(
			async () =>
			{
				for (var i = 0; i < 50; i++)
				{
					await tracker.BumpStampAsync("churned", Ct);
				}
			},
			Ct));

		await Task.WhenAll(readers.Concat(bumpers));

		var settled = await tracker.GetOrCreateStampAsync("churned", Ct);
		settled.ShouldNotBeNullOrEmpty();
		(await tracker.GetOrCreateStampAsync("churned", Ct)).ShouldBe(settled,
			"once the churn stops the tag must settle on a single stable stamp");
	}

	// ---- Cancellation. ----

	/// <summary>
	/// Both operations complete synchronously against an in-process dictionary, so an already-cancelled
	/// token has no I/O to interrupt. Pinned as observed behaviour rather than assumed: a caller that
	/// wrapped these in a cancellation-sensitive path would otherwise be relying on an untested guess.
	/// </summary>
	[Fact]
	public async Task CompleteAgainstAnAlreadyCancelledToken_HavingNoIoToAbandon()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = new InMemoryCacheTagTracker(meterFactory);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		var stamp = await tracker.GetOrCreateStampAsync("orders", cts.Token);
		await tracker.BumpStampAsync("orders", cts.Token);

		stamp.ShouldNotBeNullOrEmpty();
	}

	// ---- Independence of tags. ----

	[Fact]
	public async Task KeepTagsFullyIndependentOfOneAnother()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = new InMemoryCacheTagTracker(meterFactory);
		var orders = await tracker.GetOrCreateStampAsync("orders", Ct);
		var users = await tracker.GetOrCreateStampAsync("users", Ct);

		await tracker.BumpStampAsync("orders", Ct);

		(await tracker.GetOrCreateStampAsync("orders", Ct)).ShouldNotBe(orders);
		(await tracker.GetOrCreateStampAsync("users", Ct)).ShouldBe(users,
			"invalidating one tag must not invalidate every entry in the cache");
	}

	[Fact]
	public async Task TreatTagNamesAsOrdinalAndCaseSensitive()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		var lower = await tracker.GetOrCreateStampAsync("orders", Ct);
		var upper = await tracker.GetOrCreateStampAsync("Orders", Ct);

		upper.ShouldNotBe(lower,
			"the stamp map is ordinal by construction; folding case here would silently merge two "
			+ "tags a consumer meant to keep apart");
	}
}
