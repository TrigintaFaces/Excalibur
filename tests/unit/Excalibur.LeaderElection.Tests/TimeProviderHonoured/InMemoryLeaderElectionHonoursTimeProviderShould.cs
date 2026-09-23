// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.TimeProviderHonoured;

/// <summary>
/// Binds the promise the constructor makes: the injected time provider is what stamps the timestamps.
/// </summary>
/// <remarks>
/// <para>
/// The constructor parameter is documented as the provider "used for event timestamps", and a consumer
/// injects one precisely so leader-election behaviour is deterministic under test. Where the code read the
/// wall clock instead, that promise was kept for some timestamps and silently broken for others — which is
/// worse than never offering it, because the consumer has no way to see which is which.
/// </para>
/// <para>
/// The arms assert the property (<b>does the timestamp equal the fake's time?</b>) rather than any
/// mechanism, and the fake's epoch is the default 2000-01-01, decades from any real clock, so a stray
/// <c>DateTimeOffset.UtcNow</c> cannot coincidentally pass.
/// </para>
/// <para>
/// The second arm advances the fake between the two reads. That is what makes this more than a
/// construction check: a timestamp captured once and reused would satisfy the first arm and fail here.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryLeaderElectionHonoursTimeProviderShould
{
	private static InMemoryLeaderElection CreateSut(FakeTimeProvider clock, string instanceId = "candidate-1") =>
		new(
			resourceName: $"resource-{Guid.NewGuid():N}",
			options: Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions { InstanceId = instanceId }),
			logger: NullLogger<InMemoryLeaderElection>.Instance,
			sharedState: new InMemoryLeaderElectionSharedState(),
			timeProvider: clock);

	/// <summary>
	/// SAFETY. Registration on start must stamp from the injected provider, not the wall clock.
	/// </summary>
	[Fact]
	public async Task Stamp_candidate_registration_from_the_injected_provider()
	{
		var clock = new FakeTimeProvider();
		await using var sut = CreateSut(clock);

		await sut.StartAsync(CancellationToken.None);

		var health = await sut.GetCandidateHealthAsync(CancellationToken.None);

		health.ShouldNotBeEmpty("the candidate must register, or this arm proves nothing about its timestamp.");
		health.Single().LastUpdated.ShouldBe(clock.GetUtcNow());
	}

	/// <summary>
	/// SAFETY, and the arm that a cached timestamp cannot pass: the clock moves between the two reads.
	/// </summary>
	[Fact]
	public async Task Restamp_from_the_injected_provider_when_health_is_updated()
	{
		var clock = new FakeTimeProvider();
		await using var sut = CreateSut(clock);

		await sut.StartAsync(CancellationToken.None);
		var atRegistration = (await sut.GetCandidateHealthAsync(CancellationToken.None)).Single().LastUpdated;

		clock.Advance(TimeSpan.FromMinutes(17));
		await sut.UpdateHealthAsync(isHealthy: true, metadata: null, CancellationToken.None);

		var afterUpdate = (await sut.GetCandidateHealthAsync(CancellationToken.None)).Single().LastUpdated;

		afterUpdate.ShouldBe(clock.GetUtcNow());
		// The move must be the one the FAKE made -- equal timestamps would mean the value was cached,
		// and a wall-clock read would move by milliseconds rather than by seventeen minutes.
		(afterUpdate - atRegistration).ShouldBe(TimeSpan.FromMinutes(17));
	}

	/// <summary>
	/// PRECISION. An unhealthy update is stamped from the same provider — the branch is not special-cased.
	/// </summary>
	[Fact]
	public async Task Stamp_an_unhealthy_update_from_the_injected_provider_too()
	{
		var clock = new FakeTimeProvider();
		await using var sut = CreateSut(clock);

		await sut.StartAsync(CancellationToken.None);
		clock.Advance(TimeSpan.FromSeconds(42));

		await sut.UpdateHealthAsync(isHealthy: false, metadata: null, CancellationToken.None);

		var health = (await sut.GetCandidateHealthAsync(CancellationToken.None)).Single();
		health.IsHealthy.ShouldBeFalse();
		health.LastUpdated.ShouldBe(clock.GetUtcNow());
	}

	/// <summary>
	/// SAFETY. The election's EVENTS carry the injected provider's time, not the wall clock.
	/// </summary>
	/// <remarks>
	/// The event arguments are the surface a consumer is most likely to assert on, and they used to stamp
	/// themselves from the system clock in a property initialiser with no way to influence it — so an
	/// election could honour the provider on its own properties and still hand out wall-clock events. The
	/// clock is advanced before the event fires, so a wall-clock read cannot pass by coincidence.
	/// </remarks>
	[Fact]
	public async Task Stamp_its_events_from_the_injected_provider()
	{
		var clock = new FakeTimeProvider();
		await using var sut = CreateSut(clock);

		DateTimeOffset? becameLeaderAt = null;
		sut.BecameLeader += (_, e) => becameLeaderAt = e.Timestamp;

		clock.Advance(TimeSpan.FromHours(3));
		await sut.StartAsync(CancellationToken.None);

		becameLeaderAt.ShouldNotBeNull(
			"the candidate never became leader, so no event fired and this arm proves nothing.");
		becameLeaderAt!.Value.ShouldBe(clock.GetUtcNow());
	}

	/// <summary>
	/// LIVENESS. The default path still works when no provider is injected.
	/// </summary>
	/// <remarks>
	/// Without this, every arm above is satisfied by a type that refuses to stamp anything at all. The
	/// assertion is deliberately loose — it binds "a real time was recorded", not a particular instant.
	/// </remarks>
	[Fact]
	public async Task Stamp_from_the_system_clock_when_no_provider_is_injected()
	{
		var before = DateTimeOffset.UtcNow.AddMinutes(-1);
		await using var sut = new InMemoryLeaderElection(
			resourceName: $"resource-{Guid.NewGuid():N}",
			options: Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions { InstanceId = "candidate-default" }),
			logger: NullLogger<InMemoryLeaderElection>.Instance,
			sharedState: new InMemoryLeaderElectionSharedState());

		await sut.StartAsync(CancellationToken.None);

		var health = (await sut.GetCandidateHealthAsync(CancellationToken.None)).Single();
		health.LastUpdated.ShouldBeGreaterThan(before);
	}
}
