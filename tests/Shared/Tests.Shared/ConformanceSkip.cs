// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Tests.Shared;

/// <summary>
/// Declares the CATEGORY of a skip, so that a reader of the results can tell a fact that cannot apply
/// from a test somebody silenced.
/// </summary>
/// <remarks>
/// <para>
/// Two unrelated things produce a skip and they need opposite treatment. A SUPPRESSION is a test that
/// should run and does not: the cause is fixable, so it needs a named owner and an expiry. A CAPABILITY
/// exemption is a fact that cannot apply, because the implementation under test does not expose the
/// surface the fact is about — a property of the type system rather than a renewable decision.
/// </para>
/// <para>
/// ONLY THE CAPABILITY HALF IS EMITTED FROM HERE, and that is deliberate rather than unfinished. A
/// suppression's defining requirements are a named owner, an expiry, and renewal on fresh evidence —
/// none of which a marker in a skip message can enforce, because nothing refuses on a date it merely
/// reads. They are carried instead by the approved-skip allowlist under <c>eng/ci</c>, whose entries
/// declare all three as mandatory fields and whose expiry is checked per entry on every CI run,
/// independently of whether the test was observed skipping. So a suppression is recorded by adding an
/// entry there, not by calling a method here, and a second marker would be a parallel mechanism for a
/// decision the first one already makes.
/// </para>
/// <para>
/// The consequence to watch for: a skip whose cause IS fixable can only reach for the capability marker,
/// which reads as the stronger claim. When that happens, say so at the call site and add the allowlist
/// entry — the marker then describes how the skip was emitted, and the entry describes what is owed.
/// </para>
/// <para>
/// A test-runner summary cannot tell them apart. It reports a count and discards every reason, so both
/// read as the same number, and a suite that silences an inconvenient test looks exactly like one that
/// correctly declines to claim conformance it cannot hold. The marker below is what a CI gate matches to
/// separate the two, and it is matched LITERALLY rather than inferred from prose, because a gate that
/// guesses intent from a message reclassifies a suppression the day somebody rewords it.
/// </para>
/// <para>
/// Which is why this helper exists at all: the marker used to be hand-typed at every skip site. A typo
/// in one of them silently turned a declared exemption into an undeclared skip, or the reverse, and
/// nothing would have reported it. Emit it from here and the string has one definition.
/// </para>
/// </remarks>
public static class ConformanceSkip
{
	/// <summary>
	/// The literal a CI gate matches to recognise a declared capability exemption.
	/// </summary>
	/// <remarks>
	/// Changing this value changes the contract with every gate that reads test results. The gates that
	/// match it today are the transport-conformance skip assertion and the inline result check in the
	/// main build workflow; both compare it literally.
	/// </remarks>
	public const string CapabilityNotApplicableMarker = "[capability-not-applicable]";

	/// <summary>
	/// Skips the current test, declaring that the fact under test does not apply to this implementation.
	/// </summary>
	/// <param name="reason">
	/// Why the fact does not apply, in terms of the surface the implementation does not expose. This is
	/// read by whoever asks why a conformance suite reported fewer facts than it declares, so it states
	/// what is absent, not merely that something is.
	/// </param>
	/// <exception cref="ArgumentException">
	/// <paramref name="reason" /> is <see langword="null" />, empty, or whitespace. A marker carrying no
	/// reason is the bare count this helper exists to replace: it satisfies a gate while telling the next
	/// reader nothing, which is the one outcome worse than an undeclared skip.
	/// </exception>
	public static void CapabilityNotApplicable(string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
		{
			throw new ArgumentException(
				"A capability exemption must state why the fact does not apply. A marker with no reason "
				+ "satisfies the gate and informs nobody.",
				nameof(reason));
		}

		Assert.Skip($"{CapabilityNotApplicableMarker} {reason}");
	}
}
