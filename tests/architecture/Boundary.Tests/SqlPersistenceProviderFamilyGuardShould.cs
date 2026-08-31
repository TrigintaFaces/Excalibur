// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Boundary.Tests;

/// <summary>
/// Structural guard binding a persistence provider's self-declared family to the interface that family is
/// discovered by: a provider answering <c>ProviderType =&gt; "SQL"</c> MUST implement
/// <c>ISqlPersistenceProvider</c>.
/// </summary>
/// <remarks>
/// <para>
/// The two are independent declarations of one fact, and nothing held them together. A SQL provider that
/// implements only the bare <c>IPersistenceProvider</c> still reports itself as SQL, so it is invisible to
/// every consumer and every piece of framework code that discovers SQL providers by the family interface,
/// and it silently opts out of the batch-execution and request-validation members that family guarantees.
/// The omission is not observable from the provider's own behaviour -- it works, in isolation, exactly as
/// its author intended -- which is why it survived until someone compared the family member by member.
/// </para>
/// <para>
/// Scanned from source rather than by reflection deliberately. <c>ProviderType</c> is an instance property,
/// so reading it reflectively means constructing a provider, and constructing one means a connection string
/// and a reachable server for every SQL backend -- a guard that could then only run where all of them are up.
/// The declaration is a literal in the source, and the source is the artifact that ships.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class SqlPersistenceProviderFamilyGuardShould
{
    /// <summary>
    /// The providers that declare the SQL family today. The guard asserts the discovered population against
    /// this floor before asserting anything about its members: an assertion over an empty set passes, so a
    /// scan that silently matched nothing -- a moved directory, a reworded declaration -- would report a
    /// clean run over no providers at all, which is the one result indistinguishable from success.
    /// </summary>
    private static readonly string[] KnownSqlProviders =
    [
        "MySqlPersistenceProvider",
        "PostgresPersistenceProvider",
        "SqlServerPersistenceProvider",
    ];

    [Fact]
    public void EverySqlFamilyProvider_ImplementsTheSqlFamilyInterface()
    {
        var srcRoot = Path.Combine(TestHelpers.GetRepositoryRoot(), "src");
        Directory.Exists(srcRoot).ShouldBeTrue($"Expected source root at '{srcRoot}'.");

        var sqlProviders = Directory
            .EnumerateFiles(srcRoot, "*PersistenceProvider.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                  && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(static path => (Path: path, Text: File.ReadAllText(path)))
            .Where(static file => DeclaresSqlFamily(file.Text))
            .ToList();

        var found = sqlProviders
            .Select(static file => Path.GetFileNameWithoutExtension(file.Path))
            .Order(StringComparer.Ordinal)
            .ToList();

        string.Join(", ", found).ShouldBe(
            string.Join(", ", KnownSqlProviders),
            "The set of providers declaring ProviderType => \"SQL\" changed. If a provider was added, add it "
            + "to KnownSqlProviders so this guard covers it. If the scan found nothing, the guard is measuring "
            + "an empty set and would pass over any violation.");

        var offenders = sqlProviders
            .Where(static file => !ImplementsSqlFamilyInterface(file.Text))
            .Select(static file => Path.GetFileNameWithoutExtension(file.Path))
            .ToList();

        offenders.ShouldBeEmpty(
            "These providers report themselves as SQL and do not implement ISqlPersistenceProvider, so they "
            + "are invisible to any consumer that discovers SQL providers by the family interface and they "
            + "offer none of the batch-execution or request-validation members their siblings guarantee: "
            + string.Join(", ", offenders));
    }

    private static bool DeclaresSqlFamily(string text) =>
        Regex.IsMatch(text, """ProviderType\s*=>\s*"SQL"\s*;""", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static bool ImplementsSqlFamilyInterface(string text) =>
        Regex.IsMatch(
            text,
            @"class\s+\w+\s*:[^{]*\bISqlPersistenceProvider\b",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));
}
