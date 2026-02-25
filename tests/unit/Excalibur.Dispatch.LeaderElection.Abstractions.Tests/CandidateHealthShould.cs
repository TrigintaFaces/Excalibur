// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="CandidateHealth"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CandidateHealthShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var health = new CandidateHealth();

		// Assert
		health.CandidateId.ShouldBe(string.Empty);
		health.IsHealthy.ShouldBeFalse();
		health.HealthScore.ShouldBe(0.0);
		health.LastUpdated.ShouldBe(default);
		health.IsLeader.ShouldBeFalse();
		health.Metadata.ShouldNotBeNull();
		health.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void AllProperties_CanBeSetViaInit()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var health = new CandidateHealth
		{
			CandidateId = "node-1",
			IsHealthy = true,
			HealthScore = 0.95,
			LastUpdated = timestamp,
			IsLeader = true,
			Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["cpu"] = "45%",
				["memory"] = "60%",
			},
		};

		// Assert
		health.CandidateId.ShouldBe("node-1");
		health.IsHealthy.ShouldBeTrue();
		health.HealthScore.ShouldBe(0.95);
		health.LastUpdated.ShouldBe(timestamp);
		health.IsLeader.ShouldBeTrue();
		health.Metadata.Count.ShouldBe(2);
		health.Metadata["cpu"].ShouldBe("45%");
	}

	[Fact]
	public void Metadata_DefaultDictionary_SupportsCaseSensitiveKeys()
	{
		// Arrange
		var health = new CandidateHealth();

		// Act
		health.Metadata["Key"] = "value1";
		health.Metadata["key"] = "value2";

		// Assert — Ordinal comparer means keys are case-sensitive
		health.Metadata.Count.ShouldBe(2);
	}
}
