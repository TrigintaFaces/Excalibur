// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Common behaviour for conformance kits whose arms are gated on optional capabilities of the component
/// under test.
/// </summary>
/// <remarks>
/// <para>
/// Conformance kits verify a contract that has a required core and a set of optional capabilities. An arm
/// that exercises an optional capability has to do something when the component does not provide it, and
/// the obvious thing -- return -- is the one thing that must not happen silently: a test runner reports an
/// arm that returned early exactly as it reports an arm that ran and passed, so an unverified capability
/// and a verified one are indistinguishable in the result.
/// </para>
/// <para>
/// This type makes that distinction observable. An arm that cannot run reports through
/// <see cref="OnArmSkipped"/>, whose default records to <see cref="ConformanceArmLedger"/>. Override it to
/// route the skip into your test framework's own dynamic-skip API, or to fail: a consumer who requires every
/// capability of their component to be certified can turn an unverified arm into a failure in one place.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class ConformanceTestKit
{
	/// <summary>
	/// Called when a conformance arm cannot run because the component under test does not provide the
	/// optional capability the arm exercises.
	/// </summary>
	/// <param name="skip"> The arm that did not run, and why. </param>
	/// <remarks>
	/// The default records to <see cref="ConformanceArmLedger"/> and returns, leaving the arm to complete
	/// without asserting. Override to surface the skip natively -- <c>Assert.Skip</c> under xUnit v3,
	/// <c>Assert.Ignore</c> under NUnit -- or to throw, which certifies that every capability the component
	/// provides was reached.
	/// </remarks>
	protected virtual void OnArmSkipped(ConformanceArmSkip skip) => ConformanceArmLedger.RecordSkipped(skip);

	/// <summary>
	/// Reports that an arm did not run for want of an optional capability.
	/// </summary>
	/// <param name="arm"> The name of the arm; pass <c>nameof</c> of the arm's own method. </param>
	/// <param name="capability"> The capability that could not be obtained. </param>
	/// <param name="reason"> A human-readable account of why the arm did not run. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="arm"/> or <paramref name="reason"/> is null. </exception>
	protected void SkipArm(string arm, Type? capability, string reason)
	{
		ArgumentNullException.ThrowIfNull(arm);
		ArgumentNullException.ThrowIfNull(reason);

		OnArmSkipped(new ConformanceArmSkip(GetType().Name, arm, capability, reason));
	}

	/// <summary>
	/// Reports that an arm passed its capability gate and is running its body.
	/// </summary>
	/// <param name="arm"> The name of the arm; pass <c>nameof</c> of the arm's own method. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="arm"/> is null. </exception>
	protected void RecordArmExecuted(string arm) => ConformanceArmLedger.RecordExecuted(GetType().Name, arm);

	/// <summary>
	/// Verifies that the deriving suite exposes every arm of its kit to its test runner.
	/// </summary>
	/// <returns> A completed task; the check is synchronous and shaped as an arm so suites wire it like one. </returns>
	/// <remarks>
	/// <para>
	/// A kit carries no test-framework attributes, so nothing in it makes an arm run. Discovery is the
	/// deriving suite's job, and that design has one failure mode: an arm nobody remembers to wire never
	/// executes, and an arm that never executes cannot fail. It is indistinguishable in the results from an
	/// arm that passed — the same confusion <see cref="OnArmSkipped"/> exists to remove, arriving one level
	/// up.
	/// </para>
	/// <para>
	/// Arms are collected from the <b>abstract</b> types between the suite and this base: those are the kit
	/// layers, and a concrete suite in the middle contributes wrappers rather than arms. Collecting per type
	/// rather than from one named kit is what lets an intermediate kit layer's arms be seen at all.
	/// </para>
	/// <para>
	/// <c>IsFinal</c> excludes implicit interface implementations, which are virtual but sealed: an
	/// intermediate layer implementing a lifecycle interface would otherwise have members like
	/// <c>InitializeAsync</c> counted as arms, and the suite would be required to wire them.
	/// </para>
	/// <para>
	/// <b>This check must stay synchronous.</b> Some suites wire it as <c>void</c> and discard the
	/// returned task, which is harmless only because it throws before returning. Making it genuinely
	/// asynchronous would make those suites pass unconditionally.
	/// </para>
	/// <para>
	/// <b>This check is not trim-safe, and deliberately carries no
	/// <c>DynamicallyAccessedMembers</c> annotation.</b> It enumerates members of the runtime type of the
	/// deriving suite, which lives in the consumer's own test assembly; the walk continues through
	/// <c>Type.BaseType</c>, whose return value the annotation vocabulary cannot describe at all. Nor can
	/// the requirement be moved onto this base type: a class-level annotation here propagates to every kit,
	/// and several kits declare arms that legitimately require unreferenced code, so annotating this type
	/// turns each of those arms into a trim error rather than protecting anything. A conformance kit runs in
	/// a test host, which is not a trimmed or ahead-of-time-published configuration, and no consumer-facing
	/// path reaches this method. The empty-arms failure below is what keeps that honest: a run in which
	/// trimming had removed the arms fails loudly rather than certifying the suite.
	/// </para>
	/// <para>
	/// <b>Finding no arms is a failure, not a pass.</b> A wiring check that enumerates nothing certifies
	/// every suite it is pointed at, which is precisely the defect it exists to detect, so the empty case
	/// throws rather than returning green.
	/// </para>
	/// </remarks>
	public virtual Task ConformanceSuite_ShouldWireEveryArm()
	{
		const System.Reflection.BindingFlags Declared =
			System.Reflection.BindingFlags.Public
			| System.Reflection.BindingFlags.Instance
			| System.Reflection.BindingFlags.DeclaredOnly;

		var arms = new List<string>();

		for (var type = GetType(); type is not null && type != typeof(ConformanceTestKit); type = type.BaseType)
		{
			if (!type.IsAbstract)
			{
				continue;
			}

			arms.AddRange(
				type.GetMethods(Declared)
					.Where(static m =>
						(m.ReturnType == typeof(Task) || m.ReturnType == typeof(void))
						&& m.GetParameters().Length == 0
						&& m.IsVirtual
						&& !m.IsFinal
						&& !m.IsSpecialName
						&& !string.Equals(m.Name, nameof(ConformanceSuite_ShouldWireEveryArm), StringComparison.Ordinal))
					.Select(static m => m.Name));
		}

		if (arms.Count == 0)
		{
			throw new TestFixtureAssertionException(
				$"{GetType().Name} reached the wiring check and no arms were found to check it against. A "
				+ "wiring check that enumerates nothing passes every suite it is pointed at, which is the "
				+ "defect it exists to detect. Either this type does not derive from a conformance kit, or "
				+ "the kit's arms no longer have the shape this enumerates (public, virtual, no parameters, "
				+ "returning Task or void).");
		}

		var declaredNames = GetType().GetMethods(Declared).Select(static m => m.Name).ToList();

		// Exact name, or the arm's name plus a suffix. A substring test would let one arm's wrapper
		// satisfy a shorter arm whose name is a prefix of it -- and this kit family has such pairs, e.g.
		// ...ShouldReturnResult against ...ShouldReturnResultWithRequiredProperties. The shorter arm would
		// then never run and the check would report green, which is the defect it exists to detect.
		var unwired = arms
			.Where(arm => !declaredNames.Exists(name =>
				string.Equals(name, arm, StringComparison.Ordinal)
				|| name.StartsWith(arm + "_", StringComparison.Ordinal)))
			.OrderBy(static n => n, StringComparer.Ordinal)
			.ToList();

		if (unwired.Count > 0)
		{
			throw new TestFixtureAssertionException(
				$"{GetType().Name} does not expose {unwired.Count} of the {arms.Count} arms in this kit to its "
				+ "test runner, so they never execute and cannot fail. Declare a member per arm — a wrapper "
				+ "that calls it, or an override — and attribute it for your runner; mark it skipped there if "
				+ "you have a known gap, so the gap stays visible. Unwired: "
				+ string.Join(", ", unwired));
		}

		return Task.CompletedTask;
	}
}
