// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Records a conformance arm that did not run because the store under test does not provide the optional
/// capability the arm exercises.
/// </summary>
/// <param name="Suite"> The conformance kit that owns the arm, for example <c>OutboxStoreConformanceTestKit</c>. </param>
/// <param name="Arm"> The name of the arm that did not run. </param>
/// <param name="Capability">
/// The optional capability the arm needed and could not obtain, or <see langword="null"/> when the arm was
/// gated on something other than a capability interface.
/// </param>
/// <param name="Reason"> A human-readable account of why the arm did not run. </param>
public readonly record struct ConformanceArmSkip(string Suite, string Arm, Type? Capability, string Reason);

/// <summary>
/// Records which conformance arms ran and which were skipped for want of an optional capability, so a
/// conformance run can be asked the one question a green result does not answer: what was actually verified?
/// </summary>
/// <remarks>
/// <para>
/// A conformance arm that returns early because the store does not provide the capability it exercises is
/// indistinguishable, in every test runner, from an arm that ran and passed. Both report a pass. That is
/// tolerable when the store genuinely lacks the capability and intolerable when it has it and the arm failed
/// to find it, because the second case certifies behaviour nobody verified. This ledger is the difference
/// between those two states, made observable.
/// </para>
/// <para>
/// The ledger is process-wide and additive. It is a reporting surface, not an assertion: a kit records into
/// it, and the consumer decides what an unverified arm means for their certification. Consumers who want a
/// skip to surface natively in their runner should override the kit's <c>OnArmSkipped</c> hook and call their
/// framework's dynamic-skip API from it; the default implementation records here.
/// </para>
/// </remarks>
public static class ConformanceArmLedger
{
	private static readonly ConcurrentDictionary<string, byte> ExecutedArms = new(StringComparer.Ordinal);
	private static readonly ConcurrentDictionary<string, ConformanceArmSkip> SkippedArms = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets the arms that ran their bodies in this process, as <c>Suite.Arm</c> keys.
	/// </summary>
	/// <value> The distinct executed arms, ordered by name. </value>
	public static IReadOnlyList<string> Executed =>
		[.. ExecutedArms.Keys.OrderBy(static k => k, StringComparer.Ordinal)];

	/// <summary>
	/// Gets the arms that did not run because a capability was unavailable.
	/// </summary>
	/// <value> The distinct skipped arms, ordered by <c>Suite.Arm</c>. </value>
	public static IReadOnlyList<ConformanceArmSkip> Skipped =>
		[.. SkippedArms.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal).Select(static kvp => kvp.Value)];

	/// <summary>
	/// Records that a conformance arm passed its capability gate and is running its body.
	/// </summary>
	/// <param name="suite"> The conformance kit that owns the arm. </param>
	/// <param name="arm"> The name of the arm. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="suite"/> or <paramref name="arm"/> is null. </exception>
	public static void RecordExecuted(string suite, string arm)
	{
		ArgumentNullException.ThrowIfNull(suite);
		ArgumentNullException.ThrowIfNull(arm);

		_ = ExecutedArms.TryAdd($"{suite}.{arm}", 0);
	}

	/// <summary>
	/// Records that a conformance arm did not run for want of an optional capability.
	/// </summary>
	/// <param name="skip"> The arm that did not run, and why. </param>
	public static void RecordSkipped(in ConformanceArmSkip skip) =>
		SkippedArms[$"{skip.Suite}.{skip.Arm}"] = skip;

	/// <summary>
	/// Clears everything recorded so far.
	/// </summary>
	/// <remarks>
	/// Intended for a consumer that reports per-suite rather than per-process. The ledger is shared process
	/// state, so a reset taken while other conformance suites are running discards their records too.
	/// </remarks>
	public static void Reset()
	{
		ExecutedArms.Clear();
		SkippedArms.Clear();
	}

	/// <summary>
	/// Produces a human-readable account of what ran and what did not.
	/// </summary>
	/// <returns> A multi-line report naming every executed and every skipped arm. </returns>
	public static string Describe()
	{
		var executed = Executed;
		var skipped = Skipped;

		var executedText = executed.Count == 0
			? "  (none)"
			: string.Join(Environment.NewLine, executed.Select(static a => $"  - {a}"));
		var skippedText = skipped.Count == 0
			? "  (none)"
			: string.Join(
				Environment.NewLine,
				skipped.Select(static s =>
					$"  - {s.Suite}.{s.Arm} [{s.Capability?.Name ?? "no capability named"}]: {s.Reason}"));

		return $"Conformance arms executed ({executed.Count}):{Environment.NewLine}{executedText}"
			+ $"{Environment.NewLine}Conformance arms NOT VERIFIED ({skipped.Count}):{Environment.NewLine}{skippedText}";
	}
}
