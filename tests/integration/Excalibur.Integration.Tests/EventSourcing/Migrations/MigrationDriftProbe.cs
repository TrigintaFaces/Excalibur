// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text;

namespace Excalibur.Integration.Tests.EventSourcing.Migrations;

/// <summary>
/// The three embedded renderings of one migration id that the drift suites bind, and the guard that
/// keeps the pair of them meaningful.
/// </summary>
/// <remarks>
/// Each namespace below contains a script whose migration id is <see cref="MigrationId"/>. Applying
/// from <see cref="OriginalNamespace"/> and then re-running against <see cref="EditedNamespace"/> is a
/// migration whose body changed after it was recorded, which a migrator must refuse. Re-running against
/// <see cref="OriginalCrlfNamespace"/> is the SAME script rendered with the other line-ending
/// convention, which a migrator must accept — that is what a repository declaring its sources
/// <c>text=auto</c> hands to a package built on a different platform, and refusing it would stop a
/// consumer's service over a difference that is not one.
/// </remarks>
internal static class MigrationDriftProbe
{
	/// <summary>The migration id all three renderings share.</summary>
	internal const string MigrationId = "001_CreateDriftProbe";

	/// <summary>The body that gets recorded as applied.</summary>
	internal const string OriginalNamespace = "MigrationDriftProbe.Original";

	/// <summary>The same body, character for character, with CRLF terminators.</summary>
	internal const string OriginalCrlfNamespace = "MigrationDriftProbe.OriginalCrlf";

	/// <summary>A different body under the same migration id.</summary>
	internal const string EditedNamespace = "MigrationDriftProbe.Edited";

	/// <summary>The table the original script creates, named so a stray application is visible.</summary>
	internal const string ProbeTableName = "migration_drift_probe";

	/// <summary>
	/// Asserts the two renderings really are what the suite claims: different bytes, identical content.
	/// </summary>
	/// <remarks>
	/// Without this the line-ending arm can go vacuous without going red. A repository-wide
	/// <c>text=auto</c> rule, or a stray editor save, would check both fixtures out with the same
	/// terminators — and the test would then compare a script against itself and pass while proving
	/// nothing. The failure message names the file that pins them so the next reader does not have to
	/// rediscover the mechanism.
	/// </remarks>
	internal static void AssertRenderingsStillDiffer()
	{
		var lf = ReadRaw(OriginalNamespace);
		var crlf = ReadRaw(OriginalCrlfNamespace);

		crlf.ShouldNotBe(
			lf,
			"The LF and CRLF drift fixtures are byte-identical, so the line-ending arm of this suite is "
			+ "comparing a script against itself and proves nothing. Their terminators are pinned in "
			+ ".gitattributes; restore those entries and re-check the files out.");

		Normalize(crlf).ShouldBe(
			Normalize(lf),
			"The LF and CRLF drift fixtures differ by more than their line endings, so accepting one for "
			+ "the other would no longer demonstrate what this suite claims. Re-derive the CRLF fixture "
			+ "from the LF one.");
	}

	private static string ReadRaw(string migrationNamespace)
	{
		var resourceName = $"{migrationNamespace}.{MigrationId}.sql";
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
			?? throw new InvalidOperationException(
				$"Migration drift fixture '{resourceName}' is not embedded. Check the EmbeddedResource "
				+ "LogicalName entries in Excalibur.Integration.Tests.csproj.");

		using var reader = new StreamReader(stream, Encoding.UTF8);
		return reader.ReadToEnd();
	}

	private static string Normalize(string content) =>
		content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
