// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.LeaderElection.Postgres;
using Excalibur.LeaderElection;

using Shouldly;

using Xunit;

namespace Excalibur.Data.Postgres.Tests.Postgres;

/// <summary>Guards coherent ownership metadata across concurrent renewal publication.</summary>
/// <remarks>The common owner publishes a monotonic request-start and the complete fence/proof
/// together under one gate. The positive control uses actual concurrent publication/read;
/// the legacy torn multi-field representation must not be reintroduced.</remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresLeaderElectionRenewalTicksShould
{
	[Fact]
    public async Task ReadCoherentMonotonicProofsAcrossConcurrentRenewalPublication()
    {
        var clock = TimeProvider.System; var proof = new object(); var start = clock.GetTimestamp();
        await using var owner = new RenewalTenureDeadline(clock, TimeSpan.FromDays(1), static _ => Task.CompletedTask);
        var generation = owner.BeginAcquisition(new object());
        owner.Publish(generation, start, proof, 42, clock.GetUtcNow()).ShouldBeTrue();
        var producer = Task.Run(() => { for (var i = 0; i < 1000; i++) { owner.Confirm(generation, clock.GetTimestamp()).ShouldBeTrue(); } });
        var reader = Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                var current = owner.Current.ShouldNotBeNull();
                current.Generation.ShouldBe(generation); current.OwnershipProof.ShouldBeSameAs(proof); current.FencingToken.ShouldBe(42);
                clock.GetElapsedTime(start, current.ProofRequestStart).ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
            }
        });
        await Task.WhenAll(producer, reader);
    }

	[Fact]
	public void NotRetainTheTornMultiFieldDateTimeOffsetRepresentation()
	{
		var legacyField = typeof(PostgresLeaderElection)
			.GetField("_lastSuccessfulRenewal", BindingFlags.NonPublic | BindingFlags.Instance);

		legacyField.ShouldBeNull(
			"the pre-fix DateTimeOffset _lastSuccessfulRenewal (the torn representation) must be removed in " +
			"favour of the Interlocked long ticks field.");
	}
}
