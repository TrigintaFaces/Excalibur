// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.RegularExpressions;

namespace Boundary.Tests;

/// <summary>
/// Holds the stored tenant partition to being a VALUE: every tenant column in shipped DDL is declared
/// <c>NOT NULL</c>, or is tightened to <c>NOT NULL</c> by the same script that introduced it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the column and not the query.</b> The untenanted partition is the reserved sentinel string, never
/// <see langword="null"/>. When an absence has two representations something has to fold between them, and
/// every fold is a place the two can disagree: an audit store signed a message authentication code over a
/// null and stored the sentinel, so every untouched trail verified as tampered; a saga store resolved an
/// unscoped context to a filter of "equals null", which matches nothing, so retention deleted zero rows
/// forever while reporting success. The column declaration is the one layer every access path shares —
/// request-based and raw SQL alike — so it is the only place the invariant can be stated once.
/// </para>
/// <para>
/// <b>This does NOT count predicates, and must never be changed into something that does.</b> A rule of the
/// form "every statement carries a tenant term" is how this framework produced its worst tenancy defect: a
/// uniform pass added a term to seven statements, three of them already addressed by a primary key, after
/// which terminal marks matched nothing, messages were never marked sent, leases expired, and messages were
/// delivered again. A statement already addressed by a unique key cannot admit a foreign row, so a tenant
/// term on it is a filter whose only reachable output is a false negative. This class reads DECLARATIONS,
/// asks nothing about any <c>WHERE</c> clause, and fires only on a column that can hold a null.
/// </para>
/// <para>
/// <b>A backfill migration is allowed to pass through nullable, and is required to come out of it.</b>
/// Adding a column to a populated table is a three-step move — add nullable, backfill, tighten — so this
/// arm's subject is not the line, it is the state the script LEAVES BEHIND. A nullable declaration is a
/// violation only when the script that wrote it never tightens that column. Six such intermediate steps
/// ship today and all six tighten; a seventh that did not would be a table that stays nullable forever.
/// </para>
/// <para>
/// <b>Scope, and what it excludes.</b> Executable DDL under <c>src/</c>: <c>.sql</c> scripts, and the DDL
/// several providers build from C# string interpolation rather than shipping as a script — a
/// <c>.sql</c>-only sweep misses those providers entirely, which is how an earlier survey of this exact
/// question read clean over code it had never opened. Comments are stripped from both, so prose about a
/// column, and a doc comment rendering a table's shape, are not read as declarations. Not covered:
/// consumer-facing DDL published outside the source tree, and any table created by a store that neither
/// ships a script nor interpolates a recognised column type.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class TenantColumnsAreNotNullableShould
{
    /// <summary>
    /// A tenant partition column declaration: the column name, then a character-typed column type.
    /// </summary>
    /// <remarks>
    /// Anchored on the TYPE rather than on a keyword like <c>CREATE TABLE</c>, because the same declaration
    /// shape appears in a create, an <c>ADD COLUMN</c>, and an <c>ALTER COLUMN</c>, and a keyword-anchored
    /// scan would see only the first. Requiring a type is what separates a declaration from an index
    /// definition, a predicate, and a column list, none of which name one.
    /// </remarks>
    private static readonly Regex Declaration = new(
        "(?<![A-Za-z0-9_])[\"`\\[]?(?<col>tenant_?id)[\"`\\]]?\\s+(?:N?VARCHAR2?|NVARCHAR2|CHARACTER\\s+VARYING|TEXT|CHAR|UUID|STRING)\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>A later statement that tightens an already-declared column to <c>NOT NULL</c>.</summary>
    /// <remarks>
    /// Two verbs, because the estate ships three dialects and they do not share one. <c>ALTER COLUMN</c>
    /// covers the two that use it; Oracle spells the same act <c>MODIFY (column ...)</c>, and a pattern
    /// that knew only the first read a real tightening as absent and reported the backfill step it
    /// completes as a permanently nullable column.
    /// </remarks>
    private static readonly Regex Tightening = new(
        "(?:ALTER\\s+COLUMN\\s+|MODIFY\\s*\\(\\s*)[\"`\\[]?(?<col>tenant_?id)[\"`\\]]?\\b[^;]*?\\bNOT\\s+NULL",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// An Oracle <c>MODIFY</c> reaching the column, used to recognise a statement that PRESERVES
    /// nullability rather than declaring it.
    /// </summary>
    /// <remarks>
    /// Oracle's <c>MODIFY</c> leaves an attribute it does not name exactly as it was, so a width change
    /// written as <c>MODIFY (TENANTID VARCHAR2(64))</c> keeps the column's existing <c>NOT NULL</c> —
    /// and restating it on a column that already has it is itself an error there, so the shipped scripts
    /// deliberately omit it and say so. Reading such a statement as a nullability declaration reports
    /// three correct width-narrowing migrations as violations, which would teach the next author to
    /// write a statement Oracle rejects. Note the asymmetry with SQL Server, whose <c>ALTER COLUMN</c>
    /// genuinely does reset a column to nullable when the clause is omitted: that one stays in scope.
    /// </remarks>
    private static readonly Regex NullabilityPreservingModify = new(
        "\\bMODIFY\\s*\\(\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex NotNullClause = new(
        "\\bNOT\\s+NULL\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly IReadOnlyList<Declared> Declarations = Scan();

    // ---- CONTROL ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The scan must find the declarations it is about, in a plausible number.
    /// </summary>
    /// <remarks>
    /// Without this the arm below is satisfied by finding nothing, and finding nothing is exactly what a
    /// renamed column, a relocated script directory, or a provider that stops shipping DDL produces. An
    /// empty scan and a clean estate are indistinguishable in the result; only this arm separates them.
    /// </remarks>
    [Fact]
    public void FindTheTenantColumnDeclarationsItIsAbout_SoAnEmptyScanCannotPassTheArmBelow()
    {
        Declarations.Count.ShouldBeGreaterThan(
            60,
            $"Only {Declarations.Count} tenant column declaration(s) found under src/. The measurement that "
            + "introduced this arm found 96 across shipped scripts and interpolated DDL. A number far below "
            + "that means the column was renamed, the scripts moved, or the scan no longer reaches the "
            + "source tree — and the arm below is then green over an empty set. Repoint the pattern at the "
            + "live name; do not leave it searching for a dead one.");

        Declarations.ShouldContain(
            d => d.RelativePath.EndsWith(".cs", StringComparison.Ordinal),
            "No interpolated DDL was found. Several providers ship no .sql at all and build their schema "
            + "from C#; a scan that reaches only scripts reports a clean result over code it never opened.");
    }

    /// <summary>
    /// NON-VACUITY. The nullability reading must report a nullable declaration as nullable.
    /// </summary>
    /// <remarks>
    /// The arm below asserts an absence, and an instrument that can never say "nullable" satisfies it
    /// forever. This drives the same two functions the scan uses over a declaration written both ways.
    /// </remarks>
    [Fact]
    public void ReadANullableDeclarationAsNullable_AndATightenedOneAsTightened()
    {
        const string Nullable = "ALTER TABLE t ADD COLUMN tenant_id VARCHAR(64);";
        const string NotNull = "    tenant_id VARCHAR(64) NOT NULL DEFAULT '__untenanted__',";

        Declaration.IsMatch(Nullable).ShouldBeTrue("the pattern must see the shape it exists to reject.");
        DeclaresNotNull(Nullable, Declaration.Match(Nullable)).ShouldBeFalse();
        DeclaresNotNull(NotNull, Declaration.Match(NotNull)).ShouldBeTrue();

        Tightening.IsMatch("ALTER TABLE t ALTER COLUMN tenant_id SET NOT NULL;").ShouldBeTrue();
        Tightening.IsMatch("ALTER TABLE t MODIFY (TENANT_ID VARCHAR2(64) NOT NULL);").ShouldBeTrue(
            "the tightening verb differs by dialect; recognising only one reads a real tightening as "
            + "absent and reports the backfill step it completes as permanently nullable.");
        Tightening.IsMatch("ALTER TABLE t ALTER COLUMN tenant_id VARCHAR(64) NULL;").ShouldBeFalse(
            "a statement that re-declares the column NULL is not a tightening; reading it as one would let "
            + "a script loosen a column and still pass.");

        const string PreservingModify = "EXECUTE IMMEDIATE 'ALTER TABLE T MODIFY (TENANTID VARCHAR2(64))';";
        const string LooseningAlter = "ALTER TABLE t ALTER COLUMN tenant_id NVARCHAR(64);";

        PreservesExistingNullability(PreservingModify, Declaration.Match(PreservingModify)).ShouldBeTrue(
            "a MODIFY naming only the type leaves the column's existing NOT NULL in place, so it declares "
            + "no nullability and is not this arm's subject.");
        PreservesExistingNullability(LooseningAlter, Declaration.Match(LooseningAlter)).ShouldBeFalse(
            "an ALTER COLUMN that omits the clause genuinely resets the column to nullable in one of the "
            + "dialects shipped here, so it must stay in scope — exempting it would hide a real loosening.");
    }

    // ---- ARM: the state a script leaves behind is NOT NULL -----------------------------------------------------

    /// <summary>
    /// Every tenant column is declared <c>NOT NULL</c>, or tightened to it by the same script.
    /// </summary>
    [Fact]
    public void LeaveNoTenantColumnAbleToHoldANull()
    {
        var offenders = Declarations
            .Where(d => !d.NotNull && !d.TightenedLater)
            .Select(d =>
                $"{d.RelativePath}:{d.Line}: '{d.Column}' is declared without NOT NULL and this file never "
                + "tightens it. The untenanted partition is the reserved sentinel, never a null — a nullable "
                + "column reintroduces the second representation, and every equality predicate against it "
                + "silently returns nothing. Declare it NOT NULL, or, if this is a backfill step on a "
                + "populated table, add the ALTER that tightens it once the backfill has run.")
            .ToList();

        offenders.ShouldBeEmpty(
            offenders.Count == 0
                ? string.Empty
                : $"{offenders.Count} nullable tenant column(s):{Environment.NewLine}"
                  + string.Join(Environment.NewLine, offenders));
    }

    // ---- scanning --------------------------------------------------------------------------------------------

    private sealed record Declared(string RelativePath, int Line, string Column, bool NotNull, bool TightenedLater);

    private static IReadOnlyList<Declared> Scan()
    {
        var repositoryRoot = TestHelpers.GetRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var declared = new List<Declared>();

        foreach (var path in Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories))
        {
            var normalised = path.Replace(Path.DirectorySeparatorChar, '/');
            var isSql = normalised.EndsWith(".sql", StringComparison.OrdinalIgnoreCase);
            var isCSharp = normalised.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

            if ((!isSql && !isCSharp)
                || normalised.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                || normalised.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var raw = File.ReadAllText(path);

            if (!Declaration.IsMatch(raw))
            {
                continue;
            }

            var text = isSql ? StripSqlComments(raw) : StripCSharpComments(raw);
            var relativePath = Path.GetRelativePath(repositoryRoot, path)
                .Replace(Path.DirectorySeparatorChar, '/');

            foreach (Match match in Declaration.Matches(text))
            {
                var column = match.Groups["col"].Value;

                if (PreservesExistingNullability(text, match))
                {
                    continue;
                }

                declared.Add(new Declared(
                    RelativePath: relativePath,
                    Line: LineOf(text, match.Index),
                    Column: column,
                    NotNull: DeclaresNotNull(text, match),
                    TightenedLater: IsTightenedAfter(text, match.Index, column)));
            }
        }

        return declared;
    }

    /// <summary>
    /// Whether a declaration says <c>NOT NULL</c>, read to the end of the line the declaration sits on.
    /// </summary>
    /// <remarks>
    /// The line is the unit because every declaration in scope is written on one, and a wider window would
    /// read the NEXT column's nullability as this one's — which reports a real violation as clean, the
    /// direction a guard must never be wrong in. A declaration split across lines is read as nullable and
    /// must say so on its first line, or state its tightening.
    /// </remarks>
    private static bool DeclaresNotNull(string text, Match declaration) =>
        NotNullClause.IsMatch(LineTail(text, declaration));

    /// <summary>The remainder of the line a declaration sits on, from the declaration onwards.</summary>
    private static string LineTail(string text, Match declaration)
    {
        var lineEnd = text.IndexOf('\n', declaration.Index);

        return lineEnd < 0 ? text[declaration.Index..] : text[declaration.Index..lineEnd];
    }

    /// <summary>
    /// Whether the statement carrying <paramref name="declaration"/> preserves the column's existing
    /// nullability instead of declaring one, and so is not a subject of this arm at all.
    /// </summary>
    private static bool PreservesExistingNullability(string text, Match declaration)
    {
        var lineStart = text.LastIndexOf('\n', Math.Max(0, declaration.Index - 1)) + 1;

        return !NotNullClause.IsMatch(LineTail(text, declaration))
            && NullabilityPreservingModify.IsMatch(text[lineStart..declaration.Index]);
    }

    /// <summary>Whether the same file later tightens <paramref name="column"/> to <c>NOT NULL</c>.</summary>
    private static bool IsTightenedAfter(string text, int index, string column)
    {
        foreach (Match match in Tightening.Matches(text[index..]))
        {
            if (string.Equals(match.Groups["col"].Value, column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int LineOf(string text, int index) =>
        text.AsSpan(0, index).Count('\n') + 1;

    /// <summary>Blanks <c>--</c> line comments, so prose naming a column is not read as declaring one.</summary>
    private static string StripSqlComments(string text) =>
        Regex.Replace(text, "--[^\n]*", string.Empty, RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// Blanks C# line and block comments, including doc comments.
    /// </summary>
    /// <remarks>
    /// Doc comments are stripped deliberately: several stores render their table's shape in one, and a
    /// rendering is documentation of DDL rather than DDL, so reading it would report the same column twice
    /// and would flag a width-change note that names a column and no nullability at all. Naive about string
    /// literals, exactly as the sibling scan is, and wrong in the same safe direction — a truncated line
    /// loses its NOT NULL and is reported.
    /// </remarks>
    private static string StripCSharpComments(string text)
    {
        var builder = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '/')
            {
                var end = text.IndexOf('\n', i);
                i = end < 0 ? text.Length : end;
                continue;
            }

            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
            {
                var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? text.Length : end + 2;
                continue;
            }

            _ = builder.Append(text[i]);
            i++;
        }

        return builder.ToString();
    }
}
