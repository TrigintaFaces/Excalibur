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
/// Every store that initialises itself lazily must serialise that initialisation.
/// </summary>
/// <remarks>
/// <para>
/// All ten MongoDB stores shipped with an unsynchronised <c>EnsureInitializedAsync</c>: a bool check,
/// then a null check, then the client, database and collection assigned as three separate statements
/// with no lock of any kind. A second caller arriving between the client assignment and the collection
/// assignment sees a non-null client, skips the whole block, and dereferences a collection that is
/// still null.
/// </para>
/// <para>
/// The window is a few instructions wide, so it is intermittent and load-dependent. That is what makes
/// a behavioural test a poor guard here: the existing concurrency test for the inbox store detected the
/// defect in only 2 runs out of 12, so it would pass over a broken store most of the time, and six of
/// the ten stores had no concurrency test at all. A structural assertion has none of that variance --
/// it either finds the guard or it does not.
/// </para>
/// <para>
/// This is deliberately about the SHAPE rather than the behaviour, and it is the cheaper half of the
/// pair: it costs no container, covers every store uniformly, and catches the case a behavioural test
/// never will -- an eleventh store added later without the guard.
/// </para>
/// </remarks>
public sealed class LazyInitialisationIsGuardedTests
{
    private static readonly string RepoRoot = TestHelpers.GetRepositoryRoot();

    // Any synchronisation primitive counts. The point is that initialisation is serialised somehow,
    // not that a particular type was used -- asserting one type would fail a correct store that chose
    // a different, equally sound primitive.
    private static readonly Regex Guard = new(
        @"SemaphoreSlim|WaitAsync|\block\s*\(|Interlocked|Lazy<",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void EveryLazilyInitialisedStoreSerialisesItsInitialisation()
    {
        var offenders = new List<string>();
        var examined = 0;

        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            if (!source.Contains("EnsureInitializedAsync", StringComparison.Ordinal))
            {
                continue;
            }

            var body = ExtractMethodBody(source, "EnsureInitializedAsync");
            if (body is null)
            {
                // The type only CALLS the method; the declaration lives elsewhere.
                continue;
            }

            examined++;

            // Follow one level of delegation before concluding anything. A dozen stores keep
            // EnsureInitializedAsync as a thin disposal-check-and-forward and put the real work --
            // and the guard -- in an InitializeAsync it calls. Inspecting only the entry method
            // reported every one of those as unguarded, which would have meant "fixing" twelve
            // stores that were already correct. The question is whether initialisation is
            // serialised, not which method holds the semaphore.
            if (Guard.IsMatch(body))
            {
                continue;
            }

            var delegated = Regex.Matches(body, @"await\s+(\w*Initiali[sz]eAsync)\s*\(")
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .Select(callee => ExtractMethodBody(source, callee))
                .Any(calleeBody => calleeBody is not null && Guard.IsMatch(calleeBody));

            if (!delegated)
            {
                offenders.Add(Path.GetRelativePath(RepoRoot, file));
            }
        }

        // LIVENESS. Without this the test passes whenever the scan finds nothing -- a moved directory,
        // a renamed method, or a broken extractor would all read as "no violations". A guard that
        // cannot find its subject is not a guard, and this is the arm that fails when the assertion
        // stops being CHECKABLE, which is different from it being satisfied.
        // 43 declarations exist today. A bound of 5 would pass after a rename cost 37 of them, which
        // is the exact failure this arm was written for -- it was calibrated below where it can fire.
        examined.ShouldBeGreaterThan(
            35,
            "found almost no lazily-initialised stores to examine. Either the method was renamed or the "
            + "extractor is broken; either way this test can no longer bind the invariant it exists to "
            + "bind, so it fails rather than reporting success over an empty set.");

        // A ratchet, not a ban. the stores listed in unguarded-lazy-init-baseline.txt still carry this defect, and fixing
        // them all in one unreviewed sweep would be a worse risk than the defect: these are shipped
        // persistence types, and a bad mechanical edit to all of them at once is not recoverable by a
        // re-run. The known set is tracked by NAME rather than by count, so a fix is recorded by
        // deleting a line and a regression is a name that was never approved -- a bare count would let
        // one store be fixed while another silently regressed.
        var approved = new HashSet<string>(
            File.ReadAllLines(Path.Combine(
                    RepoRoot, "tests", "architecture", "Boundary.Tests", "Architecture",
                    "unguarded-lazy-init-baseline.txt"))
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('#')),
            StringComparer.Ordinal);

        var unapproved = offenders.Where(o => !approved.Contains(Path.GetFileName(o))).ToList();
        // Compare on file name: offenders carry a repo-relative path (so the failure message can be
        // acted on without a grep) while the baseline lists bare names. Comparing the two directly
        // made every approved entry look fixed.
        var offenderNames = offenders.Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        var fixedSince = approved.Where(a => !offenderNames.Contains(a)).ToList();

        // SAFETY.
        unapproved.ShouldBeEmpty(
            $"{unapproved.Count} store(s) newly perform lazy initialisation with no "
            + "synchronisation. Two concurrent first callers then race: one assigns the client and is "
            + "still assigning the collection when the other observes a non-null client, skips the "
            + "block, and dereferences null. Serialise it -- the established pattern here is a "
            + "SemaphoreSlim(1,1) with a volatile flag, a re-check inside the lock and Release in a "
            + $"finally -- see src/Excalibur/Excalibur.Cdc.Postgres/PostgresCdcStateStore.cs:298. New offenders: {string.Join(", ", unapproved)}");

        // Reported AFTER the regression check, deliberately. With the order reversed, a commit that
        // fixes one store and breaks another is told to tidy the list and never hears about the new
        // race -- bookkeeping hiding a defect.
        fixedSince.ShouldBeEmpty(
            $"{fixedSince.Count} store(s) are listed as unguarded but now serialise their "
            + "initialisation. Delete them from unguarded-lazy-init-baseline.txt so a later "
            + $"regression is caught. Fixed: {string.Join(", ", fixedSince)}");
    }



    [Fact]
    public void EveryGuardedStoreDeclaresItsInitialisedFlagVolatile()
    {
        // A lock alone is not sufficient, and this is the half that is easy to miss because the code
        // LOOKS complete. The fast path -- "if (_initialized) return;" -- reads the flag WITHOUT
        // taking the lock, so nothing orders that read against the field writes that preceded the
        // flag being set. The writer is fine: releasing the lock flushes its writes. The reader is
        // not, and on a weak memory model (arm64: Graviton, Apple silicon, Windows on ARM) it may
        // observe the flag as true while the client or collection it is about to use is still stale.
        //
        // volatile is what supplies the missing half: the write becomes a release, the fast-path read
        // becomes an acquire, and the fields written before the flag cannot be observed as they were
        // before it. Without it this is textbook broken double-checked locking, and it fails only on
        // hardware most contributors do not develop on -- which is exactly why it needs a test rather
        // than a convention.
        var offenders = new List<string>();
        var examined = 0;

        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            if (!source.Contains("_initLock", StringComparison.Ordinal)
                || !source.Contains("bool _initialized", StringComparison.Ordinal))
            {
                continue;
            }

            examined++;

            if (!Regex.IsMatch(source, @"\bvolatile\s+bool\s+_initialized\b"))
            {
                offenders.Add(Path.GetRelativePath(RepoRoot, file));
            }
        }

        // LIVENESS: 48 such stores exist today. Without this the test passes when the scan finds
        // nothing, and a renamed field would read as universal compliance.
        examined.ShouldBeGreaterThan(
            40,
            "found almost no lock-guarded stores to examine, so the field was probably renamed and "
            + "this test can no longer bind the invariant it exists to bind.");

        offenders.ShouldBeEmpty(
            $"{offenders.Count} store(s) guard initialisation with a lock but read the completion flag "
            + "on the fast path without a barrier. Declare it `volatile`. On x64 this is usually "
            + "benign, which is the trap: it will surface on arm64 as a store that reports itself "
            + $"initialised while its fields are not yet visible. Offenders: {string.Join(", ", offenders)}");
    }

    // ------------------------------------------------------------------------------------------
    // The extractor is the instrument every verdict above rests on, so it gets its own tests. An
    // untested extractor that silently returns the WRONG span does not fail loudly: it reports a
    // guarded store as unguarded (noisy, survivable) or -- the direction that matters -- runs past
    // the end of the method and picks up a semaphore belonging to the NEXT one, which reports an
    // unguarded store as guarded and hides exactly the defect this file exists to catch.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Extractor_Returns_The_Named_Method_Body()
    {
        var body = ExtractMethodBody(
            """
            public sealed class S
            {
                private async Task EnsureInitializedAsync(CancellationToken ct)
                {
                    await _initLock.WaitAsync(ct);
                }
            }
            """,
            "EnsureInitializedAsync");

        body.ShouldNotBeNull("the extractor could not find a plainly-declared method, so every "
            + "'no violations' verdict it has ever produced was an empty scan reading as success.");
        body.ShouldContain("WaitAsync");
    }

    [Fact]
    public void Extractor_Does_Not_Reach_Into_A_Sibling_Method()
    {
        // The bug this guards: a file-level grep once reported a store as guarded because a
        // synchronisation token appeared somewhere else in the file entirely. If the extractor
        // over-reads, that bug is back and it is invisible.
        var body = ExtractMethodBody(
            """
            public sealed class S
            {
                private async Task EnsureInitializedAsync(CancellationToken ct)
                {
                    _client = new Client();
                }

                private async Task SomethingElseAsync(CancellationToken ct)
                {
                    await _initLock.WaitAsync(ct);
                }
            }
            """,
            "EnsureInitializedAsync");

        body.ShouldNotBeNull();
        body!.Contains("WaitAsync", StringComparison.Ordinal).ShouldBeFalse(
            "the extractor ran past the end of the target method and swallowed a sibling. A guard "
            + "in ANY later method would then read as a guard on this one -- an unguarded store "
            + "reported as guarded, which is the failure direction that hides the defect.");
    }

    [Fact]
    public void Extractor_Is_Not_Confused_By_Braces_Inside_String_Literals()
    {
        // Brace counting is character-based, so a brace inside a string literal is indistinguishable
        // from a real one. An unbalanced OPEN brace in a string is the dangerous case: it inflates
        // the depth, the method never appears to close, and the extractor keeps reading into the
        // next method.
        var body = ExtractMethodBody(
            """
            public sealed class S
            {
                private async Task EnsureInitializedAsync(CancellationToken ct)
                {
                    _sql = "SELECT json_build_object('a', 1) {";
                    _client = new Client();
                }

                private async Task SomethingElseAsync(CancellationToken ct)
                {
                    await _initLock.WaitAsync(ct);
                }
            }
            """,
            "EnsureInitializedAsync");

        body.ShouldNotBeNull();
        body!.Contains("WaitAsync", StringComparison.Ordinal).ShouldBeFalse(
            "an unbalanced brace inside a string literal made the extractor read past the end of "
            + "the method and into the next one, so a sibling's semaphore now counts as this "
            + "method's guard.");
    }

    /// <summary>
    /// Returns the body of the named method, or <see langword="null"/> when the file only
    /// references it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bounded to the method rather than grepping the file: a file-level grep once reported one of
    /// these stores as guarded because a synchronisation token appeared somewhere else in it
    /// entirely. The question is whether THIS method is guarded.
    /// </para>
    /// <para>
    /// The brace matching skips comments and string literals, which is not fastidiousness. Plain
    /// character counting was wrong in the direction that hides defects: an unbalanced brace inside
    /// a string -- a SQL fragment, a JSON template -- inflated the depth, the method never appeared
    /// to close, and the scan ran on into the NEXT method. A semaphore belonging to a sibling then
    /// counted as this method's guard, so an unguarded store reported as guarded. That is a false
    /// GREEN on the exact defect this file exists to catch, and it was found by testing the
    /// extractor rather than by trusting it.
    /// </para>
    /// <para>
    /// Where it cannot be sure, it REFUSES: an unterminated body, or a body that appears to contain
    /// another method declaration, throws instead of returning a span. A wrong span produces a
    /// confident verdict, and a confident wrong verdict is worse than an error message.
    /// </para>
    /// </remarks>
    private static string? ExtractMethodBody(string source, string methodName)
    {
        var declaration = Regex.Match(
            source,
            @"(?:private|protected|internal|public)[^\r\n;]*\b" + Regex.Escape(methodName) + @"\s*\([^)]*\)\s*\{");
        if (!declaration.Success)
        {
            return null;
        }

        var openBrace = declaration.Index + declaration.Length - 1;
        var closeBrace = FindMatchingBrace(source, openBrace);

        if (closeBrace < 0)
        {
            throw new InvalidOperationException(
                $"Could not find the end of '{methodName}'. The extractor cannot bound the search to "
                + "this method, so any verdict about it would be unsound. This REFUSES rather than "
                + "returning a partial span that would read as a measurement.");
        }

        var body = source[declaration.Index..(closeBrace + 1)];

        // An over-read is silent by nature, so it is checked for. A method body cannot contain a
        // second method declaration -- local functions carry no accessibility modifier and so do
        // not match -- and if one appears, the scan has run past the end of this method.
        var nested = Regex.Count(
            body,
            @"(?:private|protected|internal|public)[^\r\n;=]*\b\w+\s*\([^)]*\)\s*\{");
        if (nested > 1)
        {
            throw new InvalidOperationException(
                $"The extracted body of '{methodName}' contains {nested} method declarations, so the "
                + "scan ran past the end of the method. A sibling's synchronisation would be "
                + "attributed to this one, which reports an unguarded store as guarded.");
        }

        return body;
    }

    /// <summary>
    /// Returns the index of the brace matching the one at <paramref name="openBrace"/>, skipping
    /// comments and every string form, or -1 if it is never closed.
    /// </summary>
    private static int FindMatchingBrace(string source, int openBrace)
    {
        var depth = 0;

        for (var i = openBrace; i < source.Length; i++)
        {
            var c = source[i];

            // Comments: braces in prose are not structure.
            if (c == '/' && i + 1 < source.Length)
            {
                if (source[i + 1] == '/')
                {
                    var newline = source.IndexOf('\n', i);
                    if (newline < 0)
                    {
                        return -1;
                    }

                    i = newline;
                    continue;
                }

                if (source[i + 1] == '*')
                {
                    var close = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (close < 0)
                    {
                        return -1;
                    }

                    i = close + 1;
                    continue;
                }
            }

            if (c == '\'')
            {
                i = SkipCharLiteral(source, i);
                if (i < 0)
                {
                    return -1;
                }

                continue;
            }

            // Verbatim strings escape a quote by doubling it, so they need their own scan.
            if (c == '@' && i + 1 < source.Length && source[i + 1] == '"')
            {
                i = SkipVerbatimString(source, i + 1);
                if (i < 0)
                {
                    return -1;
                }

                continue;
            }

            if (c == '"')
            {
                i = SkipString(source, i);
                if (i < 0)
                {
                    return -1;
                }

                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>Returns the index of the closing quote, handling raw strings, or -1.</summary>
    private static int SkipString(string source, int openQuote)
    {
        // A raw string is opened by three or more quotes and closed by a run of at least as many,
        // and a backslash is not an escape inside one.
        var fence = 0;
        while (openQuote + fence < source.Length && source[openQuote + fence] == '"')
        {
            fence++;
        }

        if (fence >= 3)
        {
            for (var i = openQuote + fence; i < source.Length; i++)
            {
                if (source[i] != '"')
                {
                    continue;
                }

                var run = 0;
                while (i + run < source.Length && source[i + run] == '"')
                {
                    run++;
                }

                if (run >= fence)
                {
                    return i + run - 1;
                }

                i += run - 1;
            }

            return -1;
        }

        for (var i = openQuote + 1; i < source.Length; i++)
        {
            if (source[i] == '\\')
            {
                i++;
                continue;
            }

            if (source[i] == '"')
            {
                return i;
            }

            if (source[i] == '\n')
            {
                // An ordinary string cannot span lines; treating it as one would swallow real code.
                return -1;
            }
        }

        return -1;
    }

    private static int SkipVerbatimString(string source, int openQuote)
    {
        for (var i = openQuote + 1; i < source.Length; i++)
        {
            if (source[i] != '"')
            {
                continue;
            }

            if (i + 1 < source.Length && source[i + 1] == '"')
            {
                i++;
                continue;
            }

            return i;
        }

        return -1;
    }

    private static int SkipCharLiteral(string source, int openQuote)
    {
        for (var i = openQuote + 1; i < source.Length && i < openQuote + 8; i++)
        {
            if (source[i] == '\\')
            {
                i++;
                continue;
            }

            if (source[i] == '\'')
            {
                return i;
            }
        }

        // Not a char literal after all -- an apostrophe in prose reaches here. Treat the quote as an
        // ordinary character rather than losing the rest of the file to it.
        return openQuote;
    }
}
