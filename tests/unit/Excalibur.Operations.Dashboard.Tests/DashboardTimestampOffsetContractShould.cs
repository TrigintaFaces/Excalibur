// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Shouldly;

using Xunit;

namespace Excalibur.Operations.Dashboard.Tests;

/// <summary>
/// Contract lock for the timestamps the dashboard read API emits.
/// </summary>
/// <remarks>
/// <para>
/// The SPA renders these values with <c>new Date(s.dueAt).toLocaleString()</c>. Per the ECMAScript
/// specification a date-time string that carries no UTC offset is interpreted as <em>local</em> time,
/// so an operator outside the server's time zone would read a due-time that is hours wrong and entirely
/// plausible. Nothing about the rendered string reveals the error.
/// </para>
/// <para>
/// The emitted instant must therefore be unambiguous. These tests bind the wire format, not the CLR
/// property type: they fail if a timestamp ever loses its offset, whichever layer discards it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DashboardTimestampOffsetContractShould
{
	/// <summary>A non-UTC, non-zero offset. A zero offset cannot distinguish "UTC" from "offset discarded".</summary>
	private static readonly TimeSpan NonZeroOffset = TimeSpan.FromHours(2);

	private static readonly DateTimeOffset DueAt = new(2026, 7, 9, 14, 30, 0, NonZeroOffset);

	[Fact]
	public void EmitAnOffsetOnDueAt_SoTheBrowserCannotParseItAsLocalTime()
	{
		var json = SerializeStuckSaga();

		var dueAt = ReadDueAtRaw(json);

		// Either an explicit +hh:mm/-hh:mm or the 'Z' designator. A bare "2026-07-09T14:30:00" is the defect.
		var carriesOffset = dueAt.EndsWith('Z')
			|| dueAt.Length >= 6 && (dueAt[^6] == '+' || dueAt[^6] == '-');

		carriesOffset.ShouldBeTrue(
			$"dueAt was emitted as '{dueAt}', which carries no UTC offset. new Date('{dueAt}') parses as " +
			"LOCAL time in the browser, so the dashboard would show the wrong due-time for any operator " +
			"outside the server's time zone.");
	}

	[Fact]
	public void PreserveTheInstantAcrossTheWire_NotMerelyTheWallClock()
	{
		// The failure this guards against does not lose the string; it loses the offset, keeping the wall
		// clock. Round-tripping and comparing the INSTANT is what detects that -- comparing the rendered
		// text would not.
		var json = SerializeStuckSaga();

		var roundTripped = JsonSerializer.Deserialize(json, SagaJsonContext.Default.StuckSagaResult);

		roundTripped.ShouldNotBeNull();
		roundTripped.Stuck.Count.ShouldBe(1);
		roundTripped.Stuck[0].DueAt.UtcDateTime.ShouldBe(DueAt.UtcDateTime);
		roundTripped.Stuck[0].DueAt.Offset.ShouldBe(NonZeroOffset);
	}

	private static string SerializeStuckSaga()
	{
		// Serialized through the very context the endpoint uses (SagaDashboardModule maps
		// Results.Json(..., SagaJsonContext.Default...)), so this binds the shipped wire format rather
		// than a serializer configured for the test.
		var result = new StuckSagaResult
		{
			Available = true,
			Stuck =
			[
				new StuckSagaView
				{
					SagaId = "saga-1",
					SagaType = "OrderSaga",
					TimeoutId = "timeout-1",
					DueAt = DueAt,
					OverdueSeconds = 90,
				}
			],
		};

		return JsonSerializer.Serialize(result, SagaJsonContext.Default.StuckSagaResult);
	}

	private static string ReadDueAtRaw(string json)
	{
		using var document = JsonDocument.Parse(json);
		return document.RootElement
			.GetProperty("stuck")[0]
			.GetProperty("dueAt")
			.GetString()!;
	}
}
