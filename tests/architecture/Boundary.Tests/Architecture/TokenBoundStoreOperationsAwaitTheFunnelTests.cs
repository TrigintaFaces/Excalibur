// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Boundary.Tests;

using Shouldly;

using Xunit;

namespace Boundary.Tests.Architecture;

/// <summary>
/// In a store that records which data-subject token function produced its records, every public
/// operation must pass through the initialisation funnel that performs the check.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this test exists, and why it is not a behavioural one.</b> A changed hashing key changes every
/// pseudonymisation token a compliance store is keyed by: an erasure then enumerates no key handles and
/// still signs a completion certificate, a legal-hold lookup finds nothing so the erasure proceeds over
/// data the law requires kept, and already-encrypted personal data stops being decryptable. The check
/// that refuses this runs once per process, at the end of <c>EnsureInitializedAsync</c>. It is only a
/// guarantee if <b>every</b> operation awaits that funnel — and the store classes have twenty-one public
/// members each, so "every one of them remembered" is not a property anyone can hold in their head.
/// </para>
/// <para>
/// <b>The alternative designs were considered and are worse.</b> Returning the token-computing capability
/// from the guard, so that skipping it would not compile, would ripple a contract change through four
/// services and still not close the path where a consumer resolves the store from the container directly.
/// Repeating the check inside every member would pay a round trip on every durable write. This assertion
/// costs nothing at runtime, needs no container, and catches the case neither of those does: a
/// twenty-second member added next year without the await.
/// </para>
/// <para>
/// <b>Deliberately structural.</b> A behavioural test would have to exercise twenty-one members per store
/// against real infrastructure to prove the same thing, and would still say nothing about a member added
/// afterwards. This either finds the await or it does not.
/// </para>
/// </remarks>
public sealed class TokenBoundStoreOperationsAwaitTheFunnelTests
{
    private static readonly string RepoRoot = TestHelpers.GetRepositoryRoot();

    /// <summary>
    /// Members that must NOT await the funnel, each for a stated reason rather than by convenience.
    /// </summary>
    private static readonly HashSet<string> Exempt = new(StringComparer.Ordinal)
    {
        // IS the funnel's public face; awaiting itself would recurse.
        "ValidateSchemaAsync",

        // Deliberately outside the funnel: initialisation calls it while HOLDING the initialisation
        // lock, so re-entering would deadlock the first operation of every process. Its public
        // counterpart does await the funnel.
        "ClaimTokenFingerprintCoreAsync",
        "VerifyTokenFingerprintAsync",
        "InitializeCoreAsync",
        "CreateSchemaIfNotExistsAsync",
        "VerifySchemaExistsAsync",
        "EnsureInitializedAsync",
    };

    private static readonly Regex PublicAsyncMember = new(
        @"^\tpublic (?:async )?(?:Task|ValueTask|IAsyncEnumerable)(?:<[^>]*>)?\s+(\w+)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

    [Fact]
    public void EveryPublicOperationOfATokenFingerprintingStoreAwaitsTheInitialisationFunnel()
    {
        var offenders = new List<string>();
        var examinedFiles = 0;
        var examinedMembers = 0;

        foreach (var file in Directory.EnumerateFiles(
            Path.Combine(RepoRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);

            // Only stores that actually record a token fingerprint are in scope. A store with no such
            // record has no check to bypass.
            if (!source.Contains("IDataSubjectTokenFingerprintLedger", StringComparison.Ordinal)
                || !source.Contains("VerifyTokenFingerprintAsync", StringComparison.Ordinal))
            {
                continue;
            }

            examinedFiles++;

            foreach (var match in PublicAsyncMember.Matches(source).Cast<Match>())
            {
                var name = match.Groups[1].Value;
                if (Exempt.Contains(name))
                {
                    continue;
                }

                examinedMembers++;

                var body = ExtractMethodBody(source, name, match.Index);
                if (body is null)
                {
                    offenders.Add($"{Path.GetFileName(file)}: {name} — body could not be read");
                    continue;
                }

                if (!body.Contains("EnsureInitializedAsync", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(file)}: {name}");
                }
            }
        }

        // NON-VACUITY. A pattern that matched nothing would report a clean pass over every store, which
        // is the exact shape of failure this repository treats as worse than no check: the numbers are
        // asserted so a regex that stops matching reddens here rather than going quietly green.
        examinedFiles.ShouldBeGreaterThanOrEqualTo(
            2,
            "both SQL providers record a token fingerprint; finding fewer means the file filter broke.");
        examinedMembers.ShouldBeGreaterThanOrEqualTo(
            20,
            "each of these stores has around twenty public operations; a much smaller number means the " +
            "member pattern stopped matching and this test is no longer looking at anything.");

        offenders.ShouldBeEmpty(
            "every public operation of a store that records a token fingerprint must await " +
            "EnsureInitializedAsync, because that is where the changed-hashing-key check runs. A member " +
            "that skips it can read or write token-keyed rows under a key the store was never verified " +
            "against — silently, and with a signed erasure certificate as the visible outcome. Offenders: "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// Returns the brace-balanced body of the member whose signature starts at <paramref name="from"/>.
    /// </summary>
    private static string? ExtractMethodBody(string source, string _, int from)
    {
        var open = source.IndexOf('{', from);
        var arrow = source.IndexOf("=>", from, StringComparison.Ordinal);
        var semicolon = source.IndexOf(';', from);

        // An expression-bodied member: its "body" is everything up to the terminating semicolon.
        if (arrow >= 0 && semicolon > arrow && (open < 0 || arrow < open))
        {
            return source[arrow..semicolon];
        }

        if (open < 0)
        {
            return null;
        }

        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[open..(i + 1)];
                }
            }
        }

        return null;
    }
}
