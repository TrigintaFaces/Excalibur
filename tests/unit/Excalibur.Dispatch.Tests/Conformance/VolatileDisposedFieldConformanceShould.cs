// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Excalibur.Dispatch.Tests.Conformance;

/// <summary>
/// Every <c>_disposed</c> flag in production source must be declared <c>volatile</c>.
/// </summary>
/// <remarks>
/// <para>
/// The flag is read on the hot path, outside any lock, by the disposal check at the top of each
/// public operation. Without the barrier nothing orders that read against the writes that preceded
/// the flag being set, so on a weak memory model a caller can observe a store as live while its
/// state is already torn down, or as disposed while it is not.
/// </para>
/// <para>
/// SCANS SOURCE, NOT LOADED ASSEMBLIES, and the reason is measured rather than stylistic. The
/// previous version of this test reflected over a hardcoded list of 18 assembly names, force-loading
/// each with Assembly.Load and swallowing FileNotFoundException. A test assembly can only load what
/// its own references put next to it, and 11 of those 18 were not there -- so the test named them,
/// failed to load them, said nothing, and scanned 7. Its own coverage arm, named for ten assemblies,
/// asserted at least SEVEN: the floor had been calibrated to the broken state, so it could never
/// fire. The result was a conformance test that reported a clean pass over code it had never looked
/// at. It was caught when it reported one of seven real violations introduced in a single change,
/// and understated it by six.
/// </para>
/// <para>
/// Reading source removes the failure mode rather than widening it: there is no load step to fail,
/// no reference graph to keep in sync, and no list to fall out of date. Every project under src is
/// covered because every file under src is read, and a project added tomorrow is covered the day it
/// is added. The cost is that this checks the DECLARATION rather than the emitted metadata, which is
/// the right trade here -- the invariant is about how the field is written, and a declaration
/// carrying the modifier compiles to a field carrying it.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Conformance")]
public sealed class VolatileDisposedFieldConformanceShould
{
	// A declaration of the flag, not a use of it: `bool _disposed;` or `bool _disposed = false;`.
	// `if (_disposed)` and `ObjectDisposedException.ThrowIf(_disposed, this)` carry no type and so
	// are not declarations.
	private static readonly Regex DisposedDeclaration = new(
		@"\bbool\s+_disposed\s*[;=]",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex VolatileModifier = new(
		@"\bvolatile\b",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	[Fact]
	public void EveryDisposedFieldIsDeclaredVolatile()
	{
		var sourceRoot = LocateSourceRoot();

		var offenders = new List<string>();
		var examined = 0;

		foreach (var file in EnumerateProductionSources(sourceRoot))
		{
			var lines = File.ReadAllLines(file);
			for (var i = 0; i < lines.Length; i++)
			{
				if (!IsDisposedDeclaration(lines[i]))
				{
					continue;
				}

				examined++;

				if (!VolatileModifier.IsMatch(lines[i]))
				{
					offenders.Add($"{Path.GetRelativePath(sourceRoot, file)}:{i + 1}  {lines[i].Trim()}");
				}
			}
		}

		// LIVENESS, and it is calibrated ABOVE the population rather than below it. The arm this
		// replaces asserted "at least 7" against a scan that saw exactly 7, which is a floor placed
		// where it can never be crossed. 264 declarations exist today; a scan that finds fewer than
		// 200 has lost whole directories and is no longer binding the invariant, whatever it reports.
		examined.ShouldBeGreaterThan(
			200,
			$"only {examined} _disposed declaration(s) were found under {sourceRoot}. The invariant "
			+ "cannot be checked over a population this small, so this fails rather than reporting a "
			+ "clean pass over a scan that has stopped reaching the code.");

		offenders.ShouldBeEmpty(
			$"{offenders.Count} _disposed field(s) are declared without volatile. The flag is read on "
			+ "the hot path outside any lock, so without the barrier a caller can observe a store as "
			+ "live while it is already torn down. Declare it 'private volatile bool _disposed;'.\n  "
			+ string.Join("\n  ", offenders));
	}

	/// <summary>
	/// The scan must be able to tell a conforming declaration from a violating one.
	/// </summary>
	/// <remarks>
	/// Without this the assertion above passes whenever the pattern matches nothing -- a renamed
	/// field, a changed convention, a regex edited into something that cannot fire. That is the
	/// failure this whole file exists downstream of, so the detector is tested rather than trusted.
	/// The liveness floor above catches a scan that finds too little; this catches a scan that finds
	/// plenty and classifies all of it wrongly.
	/// </remarks>
	[Fact]
	public void DistinguishAConformingDeclarationFromAViolatingOne()
	{
		IsViolation("	private volatile bool _disposed;").ShouldBeFalse(
			"the conforming declaration used by every store in the tree was reported as a violation, "
			+ "so this test would fail the entire repository.");

		IsViolation("	private bool _disposed;").ShouldBeTrue(
			"a plainly non-volatile declaration was NOT flagged, so the assertion in this file cannot "
			+ "detect the defect it exists to prevent and its green means nothing.");

		IsViolation("	private bool _disposed = false;").ShouldBeTrue(
			"an initialised non-volatile declaration was not flagged.");

		// Uses of the flag are not declarations, and flagging them would make the test fail
		// everywhere for the wrong reason.
		IsViolation("		if (_disposed)").ShouldBeFalse("a read of the flag is not a declaration.");
		IsViolation("		ObjectDisposedException.ThrowIf(_disposed, this);").ShouldBeFalse(
			"a disposal guard call is not a declaration.");

		// A commented-out declaration is prose. Left unhandled it would fail the build over a note.
		IsViolation("		// private bool _disposed;").ShouldBeFalse("a comment is not a declaration.");
		IsViolation("		/// <summary>private bool _disposed;</summary>").ShouldBeFalse(
			"a doc comment is not a declaration.");
	}

	private static bool IsViolation(string line) =>
		IsDisposedDeclaration(line) && !VolatileModifier.IsMatch(line);

	private static bool IsDisposedDeclaration(string line)
	{
		var trimmed = line.TrimStart();
		if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('*'))
		{
			return false;
		}

		return DisposedDeclaration.IsMatch(line);
	}

	/// <summary>
	/// Every C# file under src, excluding build output.
	/// </summary>
	/// <remarks>
	/// Scope is src only, deliberately. benchmarks/ holds one non-volatile declaration on a harness
	/// class; it is not shipped, it is disposed on the one thread that built it, and the property
	/// being enforced here is about a flag read concurrently on a hot path. Widening to cover it
	/// would fail the build over code the invariant does not apply to. samples/ has none, and would
	/// be worth including if it ever did, since a sample is code a consumer copies.
	/// </remarks>
	private static IEnumerable<string> EnumerateProductionSources(string sourceRoot) =>
		Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
			// Build output holds generated sources that are not authored here and would both inflate
			// the population and report violations nobody can fix in source.
			.Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

	/// <summary>
	/// Returns the repository's src directory, or throws.
	/// </summary>
	/// <remarks>
	/// Throws rather than returning null. A locator that yields nothing when it cannot find the tree
	/// hands its caller an empty scan, and an empty scan is indistinguishable from a clean one -- the
	/// same shape of defect this file was rewritten to remove.
	/// </remarks>
	private static string LocateSourceRoot()
	{
		var dir = AppContext.BaseDirectory;
		for (var i = 0; i < 12; i++)
		{
			var candidate = Path.Combine(dir, "src");
			if (Directory.Exists(candidate))
			{
				return candidate;
			}

			var parent = Directory.GetParent(dir);
			if (parent is null)
			{
				break;
			}

			dir = parent.FullName;
		}

		throw new DirectoryNotFoundException(
			$"Could not locate the repository's src directory by walking up from {AppContext.BaseDirectory}. "
			+ "This test cannot check the invariant without it, so it fails rather than passing over "
			+ "an empty scan.");
	}
}
