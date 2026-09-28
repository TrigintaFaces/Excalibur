// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

namespace Excalibur.Integration.Tests.Infrastructure;

/// <summary>
/// Records positive evidence that a Cosmos emulator was genuinely reached by this test run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> CI asserts that the Cosmos suites actually ran, and it cannot do that from
/// test counters: a test whose body takes an early return on an availability guard genuinely executed,
/// so it counts as executed AND passed, and no counter distinguishes it from a real verification. The
/// only signal a skip or an early return cannot produce is a record written PAST the guard, on the path
/// where the emulator answered. That is what this writes.
/// </para>
/// <para>
/// <b>Call it from the FIXTURE, not from a test.</b> A fixture owns the emulator handshake, so one call
/// at the end of a successful initialization covers every test that shares it -- and a new suite added
/// to an existing collection is covered without anyone remembering to do anything. A per-test call is
/// the version of this that silently stops covering the next class somebody writes.
/// </para>
/// <para>
/// <b>Call it only after a real round-trip.</b> A container that started is not an emulator that
/// answered; the point of emission should be past a call that would have thrown had the emulator been
/// unreachable, such as creating the database. Emitting on container start would report readiness the
/// suite has not established.
/// </para>
/// <para>
/// The record is positive evidence only. A lost line under-reports, which can only make a reader
/// conclude that less was verified than actually was; nothing here can fabricate a line for a fixture
/// that never initialized. Every failure is swallowed deliberately -- evidence collection is diagnostic
/// and must never turn a passing suite red.
/// </para>
/// </remarks>
internal static class CosmosExecutionEvidence
{
	/// <summary>
	/// Names the environment variable holding the evidence file path. Unset means nothing is written,
	/// so local development and any run that does not ask for evidence are unaffected.
	/// </summary>
	private const string EvidenceFileVariable = "COSMOS_EVIDENCE_FILE";

	/// <summary>
	/// Appends one record attesting that <paramref name="fixtureName"/> reached the emulator.
	/// </summary>
	/// <param name="fixtureName">
	/// The fixture that completed its emulator handshake. Pass <c>nameof(TheFixture)</c> so the record
	/// names a real type and cannot drift from one.
	/// </param>
	/// <remarks>
	/// The line is tab-separated as <c>{UTC timestamp, round-trip format}\t{fixture name}\t{detail}</c>.
	/// The gate reads the timestamp and the name and ignores the rest, so the third field is free for
	/// whatever a human would want when reading the file directly.
	/// </remarks>
	internal static void RecordEmulatorReached(string fixtureName)
	{
		try
		{
			var path = Environment.GetEnvironmentVariable(EvidenceFileVariable);
			if (string.IsNullOrWhiteSpace(path))
			{
				return;
			}

			var line = string.Concat(
				DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
				"\t",
				fixtureName,
				"\t",
				"emulator-handshake-completed",
				Environment.NewLine);

			File.AppendAllText(path, line);
		}
		catch (Exception)
		{
			// Diagnostic only. An unwritable path, a racing append from a sibling fixture, or a missing
			// directory must not convert a genuinely passing suite into a failure.
		}
	}
}
