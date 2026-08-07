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
/// Disposal must not be refused by the guard that exists to protect callers from disposal.
/// </summary>
/// <remarks>
/// <para>
/// The shape: a public method opens with <c>ObjectDisposedException.ThrowIf(_disposed, this)</c>,
/// and <c>DisposeAsync</c> marks the instance disposed and THEN calls that same method to do its
/// cleanup. The guard fires against disposal's own call, disposal's catch logs the rejection, and
/// the cleanup silently never happens.
/// </para>
/// <para>
/// It is quiet in every way that matters. The type still implements the interface, disposal still
/// returns, no test asserting "does it throw" can see it, and the exception is swallowed by the very
/// handler written to make disposal robust. Two providers held a database connection open and went
/// on renewing a distributed lock for the life of the process; what surfaced was not a failure but a
/// test host that would not exit, five minutes later, naming whichever test happened to be running.
/// </para>
/// <para>
/// The correct shape is already in this codebase and predates the defect: keep the guard on the
/// public entry point, put the work in a private core, and have both the public method and disposal
/// call the core. Disposal then cannot be refused by its own guard.
/// </para>
/// </remarks>
public sealed class DisposalDoesNotRouteThroughItsOwnGuardTests
{
    private static readonly string RepoRoot = TestHelpers.GetRepositoryRoot();

    private static readonly Regex DisposedGuard = new(
        @"ObjectDisposedException\.ThrowIf\s*\(\s*_disposed|if\s*\(\s*_disposed\s*\)\s*\{?\s*throw",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Declaration = new(
        @"(?:public|private|protected|internal)[^\r\n;=]*?\b(\w+)\s*\([^)]*\)\s*\{",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // A call on THIS instance. `Foo(` and `this.Foo(` reach the object being disposed; `_queue.Foo(`
    // does not. Matching the bare identifier without checking the receiver reported
    // ConcurrentQueue.TryDequeue and Dictionary.Clear as defects, which is how a structural test
    // starts costing more than it saves.
    private static readonly Regex BareCall = new(
        @"(?<![.\w])(\w+)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ThisCall = new(
        @"(?<![\w.])this\.(\w+)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MarksDisposed = new(
        @"_disposed\s*=\s*true",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] DisposalMethods = ["Dispose", "DisposeAsync", "DisposeAsyncCore"];

    // Control-flow keywords read as calls to a bare-identifier match. None can name a guarded
    // method, so excluding them only shortens the candidate set.
    private static readonly HashSet<string> NotACall = new(StringComparer.Ordinal)
    {
        "if", "while", "for", "foreach", "switch", "catch", "lock", "using", "return", "nameof", "typeof", "await",
    };

    [Fact]
    public void EveryDisposalPathAvoidsItsOwnDisposedGuard()
    {
        var offenders = new List<string>();
        var filesWithDisposedField = 0;
        var filesWithAGuardedMethod = 0;

        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var source = File.ReadAllText(file);
            if (!source.Contains("_disposed", StringComparison.Ordinal))
            {
                continue;
            }

            filesWithDisposedField++;

            if (GuardedMethodNames(source).Count > 0)
            {
                filesWithAGuardedMethod++;
            }

            offenders.AddRange(
                FindOffendingCalls(source)
                    .Select(offence => $"{Path.GetRelativePath(RepoRoot, file)}: {offence}"));
        }

        // LIVENESS. Without these the test passes whenever the scan finds nothing, and a moved
        // directory or a broken extractor reads exactly like a clean repository. 267 files carry the
        // field and 115 declare a guarded method; the floors sit well below both so ordinary churn
        // does not trip them, and well above zero so a scan that stopped working does.
        filesWithDisposedField.ShouldBeGreaterThan(
            200,
            $"only {filesWithDisposedField} files with a _disposed field were found, against 267 when "
            + "this was calibrated. The scan is no longer reaching its subject, so the empty offender "
            + "list below means nothing.");

        filesWithAGuardedMethod.ShouldBeGreaterThan(
            80,
            $"only {filesWithAGuardedMethod} files were found to declare a method guarded on _disposed, "
            + "against 115 when this was calibrated. If the guard pattern is no longer recognised then "
            + "no disposal can be found routing through it, and this test has stopped being a detector.");

        offenders.ShouldBeEmpty(
            "disposal routes through a method that rejects a disposed instance, so the cleanup in it "
            + "never runs and the rejection is swallowed by disposal's own catch:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders)
            + Environment.NewLine
            + "Move the work into a private core that carries no guard, and call the core from both the "
            + "public method and from disposal.");
    }

    /// <summary>
    /// The RED case, written down rather than waited for: this is the exact shape that shipped, and
    /// it proves the assertion above is still a detector rather than a formality.
    /// </summary>
    [Fact]
    public void DetectDisposalThatRoutesThroughTheGuard()
    {
        const string Broken = """
            internal sealed class S
            {
                private volatile bool _disposed;

                public async Task StopAsync(CancellationToken cancellationToken)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    await _cts.CancelAsync().ConfigureAwait(false);
                }

                public async ValueTask DisposeAsync()
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;

                    try
                    {
                        await StopAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogDisposeError(ex);
                    }
                }
            }
            """;

        FindOffendingCalls(Broken).ShouldNotBeEmpty(
            "the deliberately-broken subject was not detected, so the repository-wide assertion has "
            + "stopped catching the defect it was written for and would now pass over a regression.");
    }

    /// <summary>The fixed shape must NOT be reported, or the test is unusable noise.</summary>
    [Fact]
    public void AcceptDisposalThatCallsAnUnguardedCore()
    {
        const string Fixed = """
            internal sealed class S
            {
                private volatile bool _disposed;

                public async Task StopAsync(CancellationToken cancellationToken)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    await StopCoreAsync(cancellationToken).ConfigureAwait(false);
                }

                private async Task StopCoreAsync(CancellationToken cancellationToken)
                {
                    await _cts.CancelAsync().ConfigureAwait(false);
                }

                public async ValueTask DisposeAsync()
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;

                    await StopCoreAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            """;

        FindOffendingCalls(Fixed).ShouldBeEmpty(
            "the corrected shape was reported as a defect. A structural test that fails the fix it "
            + "asks for will be deleted rather than obeyed.");
    }

    /// <summary>
    /// A method on a FIELD that happens to share a name with a guarded method is not a call on the
    /// object being disposed. This was a real false positive, not a hypothetical one.
    /// </summary>
    [Fact]
    public void IgnoreASameNamedMethodReachedThroughAField()
    {
        const string FieldCall = """
            internal sealed class S
            {
                private volatile bool _disposed;

                public bool TryDequeue(out Work? work)
                {
                    if (_disposed)
                    {
                        throw new ObjectDisposedException(nameof(S));
                    }

                    return _queue.TryDequeue(out work);
                }

                public void Dispose()
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;

                    while (_queue.TryDequeue(out var work))
                    {
                        work.CompletionSource.TrySetCanceled();
                    }
                }
            }
            """;

        FindOffendingCalls(FieldCall).ShouldBeEmpty(
            "a call on a field was attributed to the object being disposed. The receiver decides "
            + "whether the guard can fire, so ignoring it turns every same-named library method into "
            + "a false report.");
    }

    /// <summary>The methods declared in <paramref name="source"/>, each paired with its text.</summary>
    /// <remarks>
    /// A method's text is bounded by the start of the NEXT declaration rather than by matching its
    /// closing brace. Brace matching has to understand every string form the language has --
    /// verbatim, raw, interpolated, and their combinations -- and on this repository it could not:
    /// it refused to bound a projection store's QueryAsync, correctly, because a literal inside the
    /// method defeated it. The next declaration is a bound that requires no such understanding.
    /// <para>
    /// It can only ever stop EARLY, never late. That matters: stopping early can miss a call, which
    /// shows up as a missed defect that the self-tests below are sized to catch, whereas running
    /// late would attribute a sibling's call to this method and report a defect that is not there.
    /// A structural test that cries wolf gets deleted, so the bound errs in the direction that
    /// keeps it trustworthy.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<(string Name, string Text)> Methods(string source)
    {
        var declarations = Declaration.Matches(source);
        var methods = new List<(string, string)>(declarations.Count);

        for (var i = 0; i < declarations.Count; i++)
        {
            var openBrace = declarations[i].Index + declarations[i].Length - 1;
            var end = i + 1 < declarations.Count ? declarations[i + 1].Index : source.Length;
            methods.Add((declarations[i].Groups[1].Value, source[openBrace..end]));
        }

        return methods;
    }

    /// <summary>Names of methods in <paramref name="source"/> that reject a disposed instance.</summary>
    private static IReadOnlyCollection<string> GuardedMethodNames(string source)
    {
        var guarded = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (name, text) in Methods(source))
        {
            // The guard is an entry condition. Looking further in would count a disposed check made
            // late, for a different reason, as a gate on the whole method.
            if (DisposedGuard.IsMatch(text[..Math.Min(400, text.Length)]))
            {
                _ = guarded.Add(name);
            }
        }

        return guarded;
    }

    /// <summary>
    /// Disposal calls, made after the instance is marked disposed, to a method that rejects a
    /// disposed instance.
    /// </summary>
    private static IReadOnlyList<string> FindOffendingCalls(string source)
    {
        var guarded = GuardedMethodNames(source);
        if (guarded.Count == 0)
        {
            return [];
        }

        var offences = new List<string>();

        foreach (var (disposalMethod, text) in Methods(source))
        {
            if (!DisposalMethods.Contains(disposalMethod, StringComparer.Ordinal))
            {
                continue;
            }

            // Only calls made AFTER the flag is set can be refused by it. A guarded method called
            // first is correct, and reporting it would be wrong.
            var marks = MarksDisposed.Match(text);
            if (!marks.Success)
            {
                continue;
            }

            var afterMarking = text[(marks.Index + marks.Length)..];

            var callees = BareCall.Matches(afterMarking).Select(m => m.Groups[1].Value)
                .Concat(ThisCall.Matches(afterMarking).Select(m => m.Groups[1].Value))
                .Distinct(StringComparer.Ordinal)
                .Where(callee => !NotACall.Contains(callee))
                .Where(callee => !DisposalMethods.Contains(callee, StringComparer.Ordinal))
                .Where(guarded.Contains);

            offences.AddRange(callees.Select(callee => $"{disposalMethod}() -> {callee}()"));
        }

        return offences;
    }
}
