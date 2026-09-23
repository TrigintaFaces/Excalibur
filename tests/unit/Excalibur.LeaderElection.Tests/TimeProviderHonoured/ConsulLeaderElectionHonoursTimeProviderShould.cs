// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Consul;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.TimeProviderHonoured;

/// <summary>
/// Binds the Consul provider's timestamps to the injected time provider, including the one a second node
/// reads.
/// </summary>
/// <remarks>
/// <para>
/// This provider carried the sharpest form of the defect: <b>one acquisition was recorded twice, from two
/// different clocks</b>. The instant written into the Consul KV — the value every other node and any
/// operator inspecting the key can see — came from the wall clock, while the instant surfaced on this
/// object's own public API came from the injected provider. Under any non-system provider those two
/// disagreed, and under a fake clock they disagree by decades.
/// </para>
/// <para>
/// So the load-bearing arm is not "each timestamp equals the fake" taken separately — it is that the
/// <b>two records of the same event agree with each other</b>. A fix that changed only the surface a test
/// happens to read would satisfy a per-field assertion and still ship two clocks.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConsulLeaderElectionHonoursTimeProviderShould
{
	/// <summary>
	/// Arranges a candidate that successfully acquires, capturing what is written to the Consul KV.
	/// </summary>
	private static async Task<(ConsulLeaderElection Sut, FakeTimeProvider Clock, List<KVPair> Acquired, List<KVPair> Put)> CreateLeaderAsync()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ConsulLeaderElectionOptions
		{
			ConsulAddress = "http://localhost:8500",
			InstanceId = "candidate-a",

			// Far longer than any arm, so no real timer fires and every stamp under test is one this
			// test provoked.
			RenewInterval = TimeSpan.FromHours(1),
		});

		var acquired = new List<KVPair>();
		var put = new List<KVPair>();

		var client = A.Fake<IConsulClient>();
		_ = A.CallTo(() => client.Session.Create(A<SessionEntry>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new WriteResult<string> { Response = "session-1" }));
		_ = A.CallTo(() => client.Session.Renew(A<string>._))
			.Returns(Task.FromResult(new WriteResult<SessionEntry> { Response = new SessionEntry() }));
		_ = A.CallTo(() => client.KV.Acquire(A<KVPair>._, A<CancellationToken>._))
			.Invokes((KVPair p, CancellationToken _) => acquired.Add(p))
			.Returns(Task.FromResult(new WriteResult<bool> { Response = true }));
		_ = A.CallTo(() => client.KV.Put(A<KVPair>._, A<CancellationToken>._))
			.Invokes((KVPair p, CancellationToken _) => put.Add(p))
			.Returns(Task.FromResult(new WriteResult<bool> { Response = true }));

		var clock = new FakeTimeProvider();
		var sut = new ConsulLeaderElection(
			"test-resource", options, client, NullLogger<ConsulLeaderElection>.Instance, fencingTokenProvider: null, clock);

		// Move the clock BETWEEN construction and acquisition. Without this the fake is frozen at its
		// epoch for the whole arm, and a timestamp captured once when the object was built would satisfy
		// every assertion below while being wrong for the acquisition it claims to describe.
		clock.Advance(TimeSpan.FromHours(7));

		await sut.StartAsync(CancellationToken.None);
		sut.IsLeader.ShouldBeTrue("arrangement failed: the candidate never acquired leadership, so nothing below is exercised.");

		return (sut, clock, acquired, put);
	}

	/// <summary>
	/// Reads the instant out of the bytes actually handed to Consul.
	/// </summary>
	/// <remarks>
	/// The payload type is internal to the provider, so this reads the wire form rather than widening
	/// production visibility for a test. That is the stronger assertion anyway: these bytes are what a
	/// second node parses, and asserting on them cannot drift from what is really written.
	/// </remarks>
	private static DateTimeOffset ReadInstant(byte[] payload, string property)
	{
		using var document = JsonDocument.Parse(payload);
		foreach (var member in document.RootElement.EnumerateObject())
		{
			if (string.Equals(member.Name, property, StringComparison.OrdinalIgnoreCase))
			{
				return member.Value.GetDateTimeOffset();
			}
		}

		throw new InvalidOperationException(
			$"the written payload has no '{property}' member, so this arm cannot compare an instant. "
			+ $"Members present: {string.Join(", ", document.RootElement.EnumerateObject().Select(m => m.Name))}.");
	}

	private static DateTimeOffset AcquiredAtFromConsulPayload(List<KVPair> acquired)
	{
		acquired.ShouldNotBeEmpty("no leader key was written, so there is no stored instant to compare.");
		return ReadInstant(acquired[^1].Value, "AcquiredAt");
	}

	/// <summary>
	/// SAFETY, and the bead's headline: the two records of one acquisition must agree.
	/// </summary>
	/// <remarks>
	/// The stored value is what a second node sees; the property is what this process reports. Before the
	/// fix these came from different clocks and disagreed by decades under a fake provider.
	/// </remarks>
	[Fact]
	public async Task Record_one_acquisition_from_one_clock()
	{
		var (sut, clock, acquired, _) = await CreateLeaderAsync();
		await using var _sut = sut;

		var storedInConsul = AcquiredAtFromConsulPayload(acquired);
		var reportedByTheApi = sut.CurrentLeadership.ShouldNotBeNull().AcquiredAt;

		storedInConsul.ShouldBe(clock.GetUtcNow());
		reportedByTheApi.ShouldBe(clock.GetUtcNow());
		storedInConsul.ShouldBe(
			reportedByTheApi,
			"the instant stored for other nodes and the instant this node reports are the same event.");
	}

	/// <summary>
	/// SAFETY. The health record written to Consul is stamped from the injected provider.
	/// </summary>
	/// <remarks>
	/// Asserted on the bytes handed to the client rather than on a field, because those bytes are what a
	/// consumer's Consul actually receives.
	/// </remarks>
	[Fact]
	public async Task Stamp_the_health_record_it_writes_from_the_injected_provider()
	{
		var (sut, clock, _, put) = await CreateLeaderAsync();
		await using var _sut = sut;

		clock.Advance(TimeSpan.FromMinutes(9));
		put.Clear();

		await sut.UpdateHealthAsync(isHealthy: true, metadata: null, CancellationToken.None);

		put.ShouldNotBeEmpty("no health key was written, so this arm would assert nothing.");
		ReadInstant(put[^1].Value, "LastUpdated").ShouldBe(clock.GetUtcNow());
	}

	/// <summary>
	/// LIVENESS. Acquisition still records a real instant when no provider is injected.
	/// </summary>
	/// <remarks>
	/// Without this, the arms above are satisfied by a provider that stops recording acquisition times.
	/// </remarks>
	[Fact]
	public async Task Record_a_real_instant_when_no_provider_is_injected()
	{
		var before = DateTimeOffset.UtcNow.AddMinutes(-1);

		var options = Microsoft.Extensions.Options.Options.Create(new ConsulLeaderElectionOptions
		{
			ConsulAddress = "http://localhost:8500",
			InstanceId = "candidate-default",
			RenewInterval = TimeSpan.FromHours(1),
		});

		var acquired = new List<KVPair>();
		var client = A.Fake<IConsulClient>();
		_ = A.CallTo(() => client.Session.Create(A<SessionEntry>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new WriteResult<string> { Response = "session-1" }));
		_ = A.CallTo(() => client.Session.Renew(A<string>._))
			.Returns(Task.FromResult(new WriteResult<SessionEntry> { Response = new SessionEntry() }));
		_ = A.CallTo(() => client.KV.Acquire(A<KVPair>._, A<CancellationToken>._))
			.Invokes((KVPair p, CancellationToken _) => acquired.Add(p))
			.Returns(Task.FromResult(new WriteResult<bool> { Response = true }));
		_ = A.CallTo(() => client.KV.Put(A<KVPair>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new WriteResult<bool> { Response = true }));

		await using var sut = new ConsulLeaderElection(
			"test-resource", options, client, NullLogger<ConsulLeaderElection>.Instance);

		await sut.StartAsync(CancellationToken.None);

		AcquiredAtFromConsulPayload(acquired).ShouldBeGreaterThan(before);
	}
}
