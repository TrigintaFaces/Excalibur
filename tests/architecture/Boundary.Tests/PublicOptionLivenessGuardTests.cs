// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.Extensions.Options;

namespace Boundary.Tests;

/// <summary>
/// Liveness guard for the public option surface: every existing gate proves an option EXISTS; this one
/// proves the shipped code READS it.
/// <para>
/// A settable public property on a public <c>*Options</c> type whose getter is called from nowhere in
/// the shipped assemblies is inert. A consumer can set it, documentation can tell them to set it, the
/// public-API baseline lists it, a configuration binder populates it, and a unit test can set it and
/// assert it reads back — and none of those can fail, because none of them asks whether the value ever
/// reaches behaviour. That is the configuration form of "a success signal is not evidence of its
/// effect", and it is the shape shared by a long run of shipped-inert-option defects.
/// </para>
/// <para>
/// This is the <b>liveness</b> half, not the behavioural half. It proves a call site exists; it does
/// not prove the value changes an outcome. The strong form — set the option, drive the real component,
/// assert observable behaviour differs — is not mechanizable across the whole surface, but the weak
/// form is, and an option with no call site at all is a provable lie rather than a suspicion. The
/// blind spots are enumerated on <see cref="OptionLivenessScanner.ScanResult"/> and must be read
/// before this guard's silence is quoted as evidence of anything.
/// </para>
/// <para>
/// The residual measured when the guard landed is enumerated per property in
/// <c>public-option-liveness-baseline.txt</c>. The guard is a one-directional ratchet: no NEW inert
/// public option can land, and fixing a listed one forces its removal from the list.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PublicOptionLivenessGuardTests
{
	private readonly ITestOutputHelper _output;

	public PublicOptionLivenessGuardTests(ITestOutputHelper output) => _output = output;

	private const string BaselineRelativePath =
		"tests/architecture/Boundary.Tests/public-option-liveness-baseline.txt";

	/// <summary>
	/// Floors for the vacuity controls. A census that silently scanned nothing reports the same clean
	/// result as a census that scanned everything, so the guard refuses below these.
	/// </summary>
	private const int MinimumAssemblies = 150;
	private const int MinimumMethodBodies = 50_000;
	private const int MinimumPopulation = 500;

	private static readonly Lazy<OptionLivenessScanner.ScanResult> ShippedScan =
		new(() => OptionLivenessScanner.Scan(ShippedAssemblyPaths()), isThreadSafe: true);

	// ---- vacuity controls ---------------------------------------------------------------------

	/// <summary>
	/// The census must be shown to have executed over the real corpus before any of its verdicts are
	/// read. An empty scan produces an empty inert list, which is indistinguishable from a clean one.
	/// </summary>
	[Fact]
	public void Census_actually_executed_over_the_shipped_assemblies()
	{
		var scan = ShippedScan.Value;

		// Printed, not merely asserted: a floor tells you the census cleared a bar, the numbers tell a
		// reader of the CI log what was actually measured.
		_output.WriteLine(
			$"option-liveness census: assemblies={scan.AssembliesScanned} methodBodies={scan.MethodBodiesWalked} " +
			$"publicOptionProperties={scan.Population.Length} unread={scan.Inert.Length} " +
			$"liveOnlyViaUnresolvedReceiver={scan.LiveOnlyViaUnresolvedReceiver.Length}");

		// The residual from the un-resolvable-receiver blind spot, printed rather than asserted: each
		// entry is a property whose ONLY evidence of liveness is a call the walker could not attribute
		// to a named receiver. They are not accused, but the size of the doubt is on the record.
		foreach (var maybe in scan.LiveOnlyViaUnresolvedReceiver)
		{
			_output.WriteLine("  live-only-via-unresolved-receiver: " + maybe.Id);
		}

		// The closed blind spot, kept as a live number: how many properties the census would have
		// certified as live purely because their own validator or post-configurer read them.
		var uncorrected = OptionLivenessScanner.Scan(ShippedAssemblyPaths(), excludeOwnPlumbing: false);

		// Enumerated, not merely counted. These are the entries whose only reader is their own
		// validator or post-configurer, so they are the sub-population where "delete" needs no
		// per-property judgement: a hidden reflective consumer would show up as a second caller.
		// A bare count cannot be worked as a batch; the identities can.
		var validatedButUnobserved = scan.Inert.Except(uncorrected.Inert)
			.OrderBy(p => p.Id, StringComparer.Ordinal)
			.ToList();

		_output.WriteLine(
			$"validated-but-unobserved (would pass as live if the own-plumbing read were credited): " +
			$"{validatedButUnobserved.Count}");

		foreach (var entry in validatedButUnobserved)
		{
			_output.WriteLine("  validated-but-unobserved: " + entry.Id);
		}

		scan.Inert.Length.ShouldBeGreaterThanOrEqualTo(uncorrected.Inert.Length,
			"excluding the option's own validation plumbing must never make MORE options look live — " +
			"if it does, the exclusion is being applied to genuine readers.");

		scan.UnreadableAssemblies.ShouldBeEmpty(
			"one or more shipped assemblies could not be read, so the census covered a partial corpus " +
			"and its silence proves nothing. Refusing rather than reporting a clean result:\n  " +
			string.Join("\n  ", scan.UnreadableAssemblies));

		scan.MalformedMethodBodies.ShouldBeEmpty(
			"the IL walker failed to consume one or more method bodies exactly, which means an " +
			"instruction length in OptionLivenessScanner.OperandSize / ExtendedOperandSize is wrong. " +
			"Calls in those bodies were not counted, so a live option could be reported inert. Fix the " +
			"opcode table:\n  " +
			string.Join("\n  ", scan.MalformedMethodBodies.Take(20)));

		scan.AssembliesScanned.ShouldBeGreaterThan(MinimumAssemblies,
			"too few shipped assemblies were scanned — the output-directory discovery is broken.");

		scan.MethodBodiesWalked.ShouldBeGreaterThan(MinimumMethodBodies,
			"too few method bodies were walked — the IL census is not reaching the shipped code.");

		scan.Population.Length.ShouldBeGreaterThan(MinimumPopulation,
			"too few public option properties were discovered — the population filter is broken.");
	}

	/// <summary>
	/// Both arms of the detector, on planted subjects, in one deterministic pass: a property nothing
	/// reads must be flagged, and a property one other type reads must not be. The probes live in this
	/// test assembly, which is outside the shipped corpus, so they never appear in the real census.
	/// </summary>
	[Fact]
	public void Detector_flags_a_planted_inert_option_and_clears_a_planted_wired_one()
	{
		var scan = OptionLivenessScanner.Scan([typeof(PublicOptionLivenessGuardTests).Assembly.Location]);

		var probeProperties = scan.Population
			.Where(p => p.DeclaringType == typeof(LivenessProbeOptions).FullName)
			.Select(p => p.PropertyName)
			.ToList();

		probeProperties.ShouldContain(nameof(LivenessProbeOptions.NeverReadByAnything),
			"the probe options type was not discovered at all, so no arm below means anything.");
		probeProperties.ShouldContain(nameof(LivenessProbeOptions.ReadByTheProbeConsumer));
		probeProperties.ShouldContain(nameof(LivenessProbeOptions.ReadOnlyByItsOwnValidator));

		var flagged = scan.Inert
			.Where(p => p.DeclaringType == typeof(LivenessProbeOptions).FullName)
			.Select(p => p.PropertyName)
			.ToList();

		// RED arm: an option wired to nothing is caught.
		flagged.ShouldContain(nameof(LivenessProbeOptions.NeverReadByAnything),
			"a planted option that nothing reads was NOT flagged — the guard cannot fail, so its " +
			"passing tells you nothing.");

		// GREEN arm: an option a real consumer reads is left alone.
		flagged.ShouldNotContain(nameof(LivenessProbeOptions.ReadByTheProbeConsumer),
			"a planted option that a consumer type genuinely reads WAS flagged — the guard accuses " +
			"live options and its output cannot be trusted.");

		flagged.ShouldContain(nameof(LivenessProbeOptions.ReadOnlyByItsOwnValidator),
			"a planted option whose ONLY reader is its own IValidateOptions implementation was NOT " +
			"flagged. Validating a value the framework never observes is precisely the lie this census " +
			"exists to catch, so crediting the validator's read certifies inertness as liveness. Fix " +
			"OptionLivenessScanner.IsOwnPlumbing / PlumbedOptionsType.");

		// The mutation arm, made permanent: with the exclusion switched off the census returns to its
		// pre-correction answer and credits the validator's read as liveness. If this stops holding, the
		// exclusion above has stopped doing anything and the arm that asserts it has gone vacuous.
		var uncorrected = OptionLivenessScanner.Scan(
			[typeof(PublicOptionLivenessGuardTests).Assembly.Location],
			excludeOwnPlumbing: false);

		uncorrected.Inert
			.Where(p => p.DeclaringType == typeof(LivenessProbeOptions).FullName)
			.Select(p => p.PropertyName)
			.ShouldNotContain(nameof(LivenessProbeOptions.ReadOnlyByItsOwnValidator),
				"with the own-plumbing exclusion disabled the census must credit the validator's read as " +
				"liveness — that is the behaviour being corrected. It does not, so the corrected arm above " +
				"proves nothing: it would pass with the fix removed.");

		flagged.ShouldNotContain(nameof(LivenessProbeOptions.ReadByAnUnrelatedValidator),
			"a planted option read by a validator of a DIFFERENT options type WAS flagged. The plumbing " +
			"exclusion must bind to the options type in the interface's generic argument, otherwise a " +
			"validator that legitimately consumes another type's option makes that option look inert.");
	}

	// ---- the ratchet ---------------------------------------------------------------------------

	[Fact]
	public void No_new_inert_public_option_outside_the_baseline()
	{
		var scan = ShippedScan.Value;
		var baseline = ReadBaseline();

		var newlyInert = scan.Inert
			.Select(p => p.Id)
			.Where(id => !baseline.Contains(id))
			.OrderBy(id => id, StringComparer.Ordinal)
			.ToList();

		newlyInert.ShouldBeEmpty(
			$"{newlyInert.Count} public option propert(ies) are settable by a consumer but their getter " +
			"is called from nowhere in the shipped assemblies. Setting them provably cannot change any " +
			"behaviour. Wire each one to the component it claims to configure, or delete it. Adding it " +
			$"to {BaselineRelativePath} requires a per-entry rationale and is a reviewable act, not a " +
			"way to make this pass:\n  " +
			string.Join("\n  ", newlyInert));
	}

	[Fact]
	public void Baseline_entries_are_still_inert()
	{
		var scan = ShippedScan.Value;
		var baseline = ReadBaseline();
		var inert = scan.Inert.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
		var population = scan.Population.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);

		var nowWired = baseline
			.Where(id => population.Contains(id) && !inert.Contains(id))
			.OrderBy(id => id, StringComparer.Ordinal)
			.ToList();

		nowWired.ShouldBeEmpty(
			"baselined option propert(ies) are now read by shipped code. Remove them from " +
			$"{BaselineRelativePath} so the list only ever shrinks:\n  " +
			string.Join("\n  ", nowWired));
	}

	[Fact]
	public void Baseline_entries_are_still_public_option_properties()
	{
		var scan = ShippedScan.Value;
		var baseline = ReadBaseline();
		var population = scan.Population.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);

		var stale = baseline
			.Where(id => !population.Contains(id))
			.OrderBy(id => id, StringComparer.Ordinal)
			.ToList();

		stale.ShouldBeEmpty(
			"baseline lists option propert(ies) that are no longer a public settable property on a " +
			$"public *Options type — remove the stale entries from {BaselineRelativePath}:\n  " +
			string.Join("\n  ", stale));
	}

	// ---- discovery ------------------------------------------------------------------------------

	/// <summary>
	/// The shipped framework assemblies, taken from this test's own output directory. Boundary.Tests
	/// project-references every project under <c>src/</c>, so that directory is the complete shipped
	/// closure and needs no hand-maintained list. Source generators are Roslyn components, not shipped
	/// runtime surface, and carry no consumer options.
	/// </summary>
	private static IEnumerable<string> ShippedAssemblyPaths() =>
		Directory.EnumerateFiles(AppContext.BaseDirectory, "Excalibur*.dll")
			.Where(path => !Path.GetFileName(path).Contains("SourceGenerators", StringComparison.OrdinalIgnoreCase))
			.OrderBy(path => path, StringComparer.Ordinal);

	private static ImmutableHashSet<string> ReadBaseline()
	{
		var path = Path.Combine(TestHelpers.GetRepositoryRoot(), BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));

		File.Exists(path).ShouldBeTrue($"the baseline file is missing: {BaselineRelativePath}");

		return File.ReadAllLines(path)
			.Select(line => line.Trim())
			.Where(line => line.Length > 0 && !line.StartsWith('#'))
			.ToImmutableHashSet(StringComparer.Ordinal);
	}
}

/// <summary>
/// Detector probe, not a framework type. Its two properties are the guard's RED and GREEN arms: one is
/// deliberately wired to nothing, the other is deliberately read by <see cref="LivenessProbeConsumer"/>.
/// Deleting either, or deleting the read below, must turn
/// <see cref="PublicOptionLivenessGuardTests.Detector_flags_a_planted_inert_option_and_clears_a_planted_wired_one"/>
/// red — that is the point of them.
/// </summary>
public sealed class LivenessProbeOptions
{
	/// <summary>Planted inert option. Nothing anywhere reads this, and nothing ever should.</summary>
	public bool NeverReadByAnything { get; set; }

	/// <summary>Planted wired option, read by <see cref="LivenessProbeConsumer"/>.</summary>
	public bool ReadByTheProbeConsumer { get; set; }

	/// <summary>
	/// Planted validated-but-unobserved option: <see cref="LivenessProbeOptionsValidator"/> is its only
	/// reader. A consumer can set it, the framework will check it, and no behaviour will ever see it —
	/// so the census must report it inert.
	/// </summary>
	public bool ReadOnlyByItsOwnValidator { get; set; }

	/// <summary>
	/// Planted cross-type read: the only reader is a validator of a DIFFERENT options type, which is a
	/// genuine read. Guards the plumbing exclusion against being applied by caller shape rather than by
	/// the options type it is bound to.
	/// </summary>
	public bool ReadByAnUnrelatedValidator { get; set; }
}

/// <summary>
/// Detector probe, not a framework type. The option's own validator — the call site that used to be
/// credited as liveness.
/// </summary>
public sealed class LivenessProbeOptionsValidator : IValidateOptions<LivenessProbeOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, LivenessProbeOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.ReadOnlyByItsOwnValidator
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail("probe");
	}
}

/// <summary>
/// Detector probe, not a framework type. A validator of an unrelated options type that reads a
/// <see cref="LivenessProbeOptions"/> property — a genuine read the exclusion must not swallow.
/// </summary>
public sealed class UnrelatedProbeOptionsValidator : IValidateOptions<UnrelatedProbeOptions>
{
	private readonly LivenessProbeOptions _other = new();

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, UnrelatedProbeOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return _other.ReadByAnUnrelatedValidator
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail("probe");
	}
}

/// <summary>Detector probe, not a framework type. The unrelated validator's own options type.</summary>
public sealed class UnrelatedProbeOptions
{
	/// <summary>Read by nothing; present only so the unrelated validator has a type to be bound to.</summary>
	public bool Unused { get; set; }
}

/// <summary>The probe's consumer: a type other than the options type that genuinely reads one option.</summary>
public static class LivenessProbeConsumer
{
	/// <summary>Reads <see cref="LivenessProbeOptions.ReadByTheProbeConsumer"/> so the GREEN arm has a real call site.</summary>
	public static string Describe(LivenessProbeOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.ReadByTheProbeConsumer ? "on" : "off";
	}
}
