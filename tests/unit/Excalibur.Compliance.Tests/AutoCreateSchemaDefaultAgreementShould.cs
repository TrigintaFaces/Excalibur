// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Excalibur.Tests.Compliance;

/// <summary>
/// Governance lock: every store that exposes an <c>AutoCreateSchema</c> option must agree on the same
/// first-run default, so a consumer wiring several stores gets one behaviour rather than a different one
/// per store.
/// </summary>
/// <remarks>
/// <para>
/// The agreed default is <see langword="false"/> — the store verifies its schema and fails fast rather than
/// issuing DDL against a consumer's database on their behalf, with provisioning an explicit opt-in. The
/// published compliance documentation states that default to consumers, so a store initialising the property
/// to <see langword="true"/> is a divergence from the shipped contract, not merely an inconsistency.
/// </para>
/// <para>
/// The check is a source-text scan of <c>src/**/*.cs</c> rather than a reflection census, and deliberately so:
/// a reflection census sees only the assemblies this test project happens to reference, so a NEW provider
/// store in a package nothing here references would be invisible to it — which is precisely the case the lock
/// exists to catch. Scanning the source tree covers every store, referenced or not.
/// </para>
/// </remarks>
[Trait("Category", "Governance")]
[Trait("Component", "Compliance")]
public sealed class AutoCreateSchemaDefaultAgreementShould
{
	/// <summary>
	/// Matches a property declaration for <c>AutoCreateSchema</c>, capturing any initialiser. The initialiser
	/// group carries its own trailing semicolon because an auto-property WITHOUT one has no semicolon at all
	/// (<c>{ get; set; }</c>), so a pattern that requires a terminating <c>;</c> matches none of the six stores
	/// that rely on the implicit default -- it reports zero and reads exactly like a clean tree.
	/// </summary>
	private static readonly Regex Declaration = new(
		@"public\s+bool\s+AutoCreateSchema\s*\{\s*get;\s*set;\s*\}\s*(?:=\s*(?<init>[^;]+);)?",
		RegexOptions.Compiled | RegexOptions.CultureInvariant,
		TimeSpan.FromSeconds(5));

	[Fact]
	public void EveryStoreExposingTheOption_DefaultsItToFalse()
	{
		var repoRoot = FindRepoRoot();
		repoRoot.ShouldNotBeNull("Could not locate repository root (walked up from the test base directory looking for .git or .beads)");

		var srcRoot = Path.Combine(repoRoot, "src");
		Directory.Exists(srcRoot).ShouldBeTrue($"Expected src/ directory at {srcRoot}");

		var declaringFiles = new List<string>();
		var violations = new List<string>();

		foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
		{
			if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
				file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			{
				continue;
			}

			foreach (Match match in Declaration.Matches(File.ReadAllText(file)))
			{
				declaringFiles.Add(Path.GetRelativePath(repoRoot, file));

				var initialiser = match.Groups["init"].Value.Trim();
				if (initialiser.Length != 0 && !initialiser.Contains("false", StringComparison.Ordinal))
				{
					violations.Add($"{Path.GetRelativePath(repoRoot, file)}: AutoCreateSchema is initialised '= {initialiser}' (expected the agreed default, false)");
				}
			}
		}

		// Liveness. If the census finds nothing -- the property renamed, the scan pointed at the wrong root --
		// the arm above passes by examining zero declarations and proves nothing. Seven stores expose it today.
		declaringFiles.Count.ShouldBeGreaterThanOrEqualTo(
			7,
			$"expected at least the seven known AutoCreateSchema declarations, found {declaringFiles.Count}: {string.Join(", ", declaringFiles)}");

		violations.ShouldBeEmpty();
	}

	private static string? FindRepoRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
				Directory.Exists(Path.Combine(dir.FullName, ".beads")))
			{
				return dir.FullName;
			}

			dir = dir.Parent;
		}

		return null;
	}
}
