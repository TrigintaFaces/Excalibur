// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

namespace Excalibur.Data.Spanner.Tests;

/// <summary>
/// Locks for <see cref="SpannerOptions"/>. The one piece of behaviour here is
/// <see cref="SpannerOptions.DatabasePath"/>, and it is load-bearing: it is the string the connection is
/// built from, Google's API rejects any other shape, and a transposed segment resolves to a database that
/// may well exist and hold somebody else's data.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SpannerOptionsShould
{
	[Fact]
	public void ComposeTheThreePartDatabasePath_InGoogleResourceOrder()
	{
		var options = new SpannerOptions
		{
			ProjectId = "acme-prod",
			InstanceId = "eu-west",
			DatabaseId = "orders",
		};

		options.DatabasePath.ShouldBe("projects/acme-prod/instances/eu-west/databases/orders");
	}

	/// <summary>
	/// Distinguishes the three segments from one another. Three values that differ only by position will
	/// still assemble into a plausible-looking path if two of them are swapped, so the arm above is checked
	/// here against a permutation the correct code cannot produce.
	/// </summary>
	[Fact]
	public void PlaceEachIdentifierInItsOwnSegment()
	{
		var options = new SpannerOptions
		{
			ProjectId = "one",
			InstanceId = "two",
			DatabaseId = "three",
		};

		var segments = options.DatabasePath.Split('/');

		segments.Length.ShouldBe(6);
		segments[0].ShouldBe("projects");
		segments[1].ShouldBe("one");
		segments[2].ShouldBe("instances");
		segments[3].ShouldBe("two");
		segments[4].ShouldBe("databases");
		segments[5].ShouldBe("three");
	}

	/// <summary>
	/// <see cref="SpannerOptions.DatabasePath"/> is computed, not cached at construction. Configuration
	/// binding assigns the segments after the instance exists, so a path snapshotted in a field would be
	/// composed of empty strings for every consumer that binds from configuration.
	/// </summary>
	[Fact]
	public void RecomputeTheDatabasePath_AfterTheSegmentsAreAssigned()
	{
		var options = new SpannerOptions();

		options.DatabasePath.ShouldBe("projects//instances//databases/");

		options.ProjectId = "p";
		options.InstanceId = "i";
		options.DatabaseId = "d";

		options.DatabasePath.ShouldBe("projects/p/instances/i/databases/d");
	}

	/// <summary>
	/// The defaults are a published contract — a consumer who sets neither retry knob inherits these — so
	/// they are pinned rather than left to drift.
	/// </summary>
	[Fact]
	public void DefaultToFiveAbortRetriesWithA25MillisecondBase()
	{
		var options = new SpannerOptions();

		options.MaxAbortRetries.ShouldBe(5);
		options.AbortRetryBaseDelayMilliseconds.ShouldBe(25);
		options.EmulatorHost.ShouldBeNull();
	}
}
