// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.EventSourcing;

namespace Excalibur.EventSourcing.Tests.Schema;

/// <summary>
/// Pins exactly what a migration checksum treats as the same script and what it treats as a change.
/// </summary>
/// <remarks>
/// The provider suites prove the comparison happens against a real database. This one pins the
/// boundary, because that boundary decides whether a service starts: too strict and a routine
/// cross-platform upgrade halts a consumer's deployment; too loose and an edited migration passes for
/// the one that was applied.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Migrations")]
public sealed class MigrationScriptChecksumShould
{
	private const string Script = "CREATE TABLE probe (id INT NOT NULL);\nINSERT INTO probe VALUES (1);\n";

	[Fact]
	public void TreatTheSameScriptAsTheSameScript()
	{
		MigrationScriptChecksum.Matches(MigrationScriptChecksum.Compute(Script), Script).ShouldBeTrue();
	}

	[Fact]
	public void TreatACrlfCheckoutOfOneScriptAsThatScript()
	{
		// A repository declaring its sources text=auto hands out CRLF on one platform and LF on another
		// for byte-identical committed content, so a package built on Windows and a package built on
		// Linux embed different bytes for the same migration. Reporting that as drift would refuse to
		// start a service whose schema is entirely correct.
		var crlf = Script.Replace("\n", "\r\n", StringComparison.Ordinal);

		MigrationScriptChecksum.Compute(crlf).ShouldBe(MigrationScriptChecksum.Compute(Script));
	}

	[Fact]
	public void TreatALoneCarriageReturnCheckoutAsThatScript()
	{
		var cr = Script.Replace("\n", "\r", StringComparison.Ordinal);

		MigrationScriptChecksum.Compute(cr).ShouldBe(MigrationScriptChecksum.Compute(Script));
	}

	[Fact]
	public void AcceptAChecksumRecordedBeforeNormalizationExisted()
	{
		// Released versions hashed the resource as read. On a package built where the checkout is CRLF
		// that is the CRLF rendering, and every database migrated by such a build holds it. Rejecting
		// it would make this check an upgrade-blocker for exactly the consumers it exists to protect.
		var legacyCrlfChecksum = LegacyRawChecksum(Script.Replace("\n", "\r\n", StringComparison.Ordinal));

		MigrationScriptChecksum.Matches(legacyCrlfChecksum, Script).ShouldBeTrue();
	}

	[Fact]
	public void AcceptAChecksumRecordedInLowerCaseHex()
	{
		var lowerCase = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Script)));

		MigrationScriptChecksum.Matches(lowerCase, Script).ShouldBeTrue();
	}

	[Theory]
	[InlineData("CREATE TABLE probe (id INT NOT NULL);\nINSERT INTO probe VALUES (2);\n", "a changed literal")]
	[InlineData("CREATE TABLE probe (id INT NULL);\nINSERT INTO probe VALUES (1);\n", "a changed column constraint")]
	[InlineData("CREATE TABLE probe (id INT NOT NULL);\n", "a removed statement")]
	[InlineData("CREATE TABLE probe (id INT NOT NULL);\nINSERT INTO probe VALUES (1);\nDROP TABLE probe;\n", "an added statement")]
	[InlineData("-- a new comment\nCREATE TABLE probe (id INT NOT NULL);\nINSERT INTO probe VALUES (1);\n", "an added comment")]
	public void RejectAnEditedBody(string edited, string because)
	{
		MigrationScriptChecksum.Matches(MigrationScriptChecksum.Compute(Script), edited)
			.ShouldBeFalse($"The comparison must report {because} as drift.");
	}

	[Fact]
	public void RejectTrailingWhitespaceAsAChange()
	{
		// Trailing whitespace looks as cosmetic as a line ending and is not: the same characters sit
		// inside a multi-line string literal, where they are part of the value. Line endings are folded
		// away because a checkout rewrites them without anyone's involvement; nothing rewrites this.
		MigrationScriptChecksum.Matches(MigrationScriptChecksum.Compute(Script), Script + "   ")
			.ShouldBeFalse();
	}

	[Fact]
	public void NameEveryDriftedMigrationInItsRefusal()
	{
		// The message is what an operator has to act on at three in the morning, so it must say which
		// migrations disagree and what the two ways out are.
		var message = MigrationScriptChecksum.DescribeDrift(["003_Something", "004_SomethingElse"]);

		message.ShouldContain("003_Something");
		message.ShouldContain("004_SomethingElse");
		message.ShouldContain("NEW numbered migration");
		message.ShouldContain("No migrations were run.");
	}

	/// <summary>
	/// Reproduces the pre-normalization checksum: a raw SHA-256 over the bytes exactly as read.
	/// </summary>
	private static string LegacyRawChecksum(string content) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
