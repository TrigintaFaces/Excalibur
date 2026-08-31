// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Workflows.Analyzers;

/// <summary>
/// Flags non-deterministic API usage inside a durable workflow body — ambient clock reads, identifier and
/// random-number generation, wall-clock delays, and elapsed-time counters — all of which diverge on
/// deterministic replay. Where a deterministic <c>IWorkflowContext</c> primitive is a drop-in substitute the
/// diagnostic carries the replacement so a code fix can apply it; otherwise it carries guidance only.
/// Reports <see cref="WorkflowDiagnosticIds.NonDeterministicApiInWorkflow"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonDeterministicApiInWorkflowAnalyzer : DiagnosticAnalyzer
{
    private const string WorkflowAttributeFullName = "Excalibur.Workflows.WorkflowAttribute";
    private const string UtcNowMethod = "UtcNowAsync";
    private const string NewGuidMethod = "NewGuidAsync";
    private const string RandomTypeName = "System.Random";

    /// <summary>
    /// Describes how a non-deterministic member is reported: the guidance shown to the developer, and the
    /// <c>IWorkflowContext</c> method a code fix may mechanically substitute — <see langword="null"/> when no
    /// drop-in substitution exists.
    /// </summary>
    /// <remarks>
    /// <see cref="CodeFixMethod"/> is only set for parameterless primitives (<c>ctx.UtcNowAsync(ct)</c>,
    /// <c>ctx.NewGuidAsync(ct)</c>). A member whose replacement needs arguments — such as a delay carrying a
    /// <see cref="System.TimeSpan"/> — must stay <see langword="null"/>, because the code fix rewrites the call
    /// as a parameterless invocation and would otherwise silently discard those arguments.
    /// </remarks>
    private readonly struct Replacement
    {
        public Replacement(string guidance, string? codeFixMethod)
        {
            Guidance = guidance;
            CodeFixMethod = codeFixMethod;
        }

        public string Guidance { get; }

        public string? CodeFixMethod { get; }
    }

    // Maps a non-deterministic (containing-type full name, member name) to its guidance/replacement.
    // Keyed on the BCL member so matching is semantic, not textual.
    private static readonly Dictionary<(string TypeName, string MemberName), Replacement> NonDeterministicMembers =
        new()
        {
            // Ambient clock — journalled and replayed by ctx.UtcNowAsync.
            [("System.DateTime", "Now")] = new("ctx.UtcNowAsync", UtcNowMethod),
            [("System.DateTime", "UtcNow")] = new("ctx.UtcNowAsync", UtcNowMethod),
            [("System.DateTime", "Today")] = new("ctx.UtcNowAsync", UtcNowMethod),
            [("System.DateTimeOffset", "Now")] = new("ctx.UtcNowAsync", UtcNowMethod),
            [("System.DateTimeOffset", "UtcNow")] = new("ctx.UtcNowAsync", UtcNowMethod),

            // Identifier generation — journalled and replayed by ctx.NewGuidAsync.
            [("System.Guid", "NewGuid")] = new("ctx.NewGuidAsync", NewGuidMethod),

            // Time-ordered GUIDs still read the ambient clock, so they diverge on replay just as Guid.NewGuid does.
            [("System.Guid", "CreateVersion7")] = new("ctx.NewGuidAsync", NewGuidMethod),

            // Wall-clock delays. The replacement takes the delay as an argument, so no mechanical code fix.
            [("System.Threading.Tasks.Task", "Delay")] = new("ctx.CreateTimerAsync(delay, cancellationToken)", null),
            [("System.Threading.Thread", "Sleep")] = new("ctx.CreateTimerAsync(delay, cancellationToken)", null),

            // Randomness. No deterministic context primitive exists: generate the value in an activity, whose
            // result the engine journals and replays.
            [(RandomTypeName, "Shared")] = new("ctx.CallActivityAsync to generate the value in an activity", null),
            [("System.Security.Cryptography.RandomNumberGenerator", "Create")] =
                new("ctx.CallActivityAsync to generate the value in an activity", null),
            [("System.Security.Cryptography.RandomNumberGenerator", "GetBytes")] =
                new("ctx.CallActivityAsync to generate the value in an activity", null),
            [("System.Security.Cryptography.RandomNumberGenerator", "Fill")] =
                new("ctx.CallActivityAsync to generate the value in an activity", null),
            [("System.Security.Cryptography.RandomNumberGenerator", "GetInt32")] =
                new("ctx.CallActivityAsync to generate the value in an activity", null),

            // Elapsed-time counters advance with real time and never replay identically.
            [("System.Environment", "TickCount")] = new("ctx.UtcNowAsync", UtcNowMethod),
            [("System.Environment", "TickCount64")] = new("ctx.UtcNowAsync", UtcNowMethod),

            [("System.Diagnostics.Stopwatch", "GetTimestamp")] = new("ctx.UtcNowAsync", UtcNowMethod),
            [("System.Diagnostics.Stopwatch", "StartNew")] = new("ctx.UtcNowAsync", UtcNowMethod),
        };

    // Cheap syntactic pre-filter: the member names above, matched before any semantic lookup.
    private static readonly HashSet<string> CandidateMemberNames =
        new()
        {
            "Now", "UtcNow", "Today", "NewGuid", "CreateVersion7",
            "Delay", "Sleep", "Shared",
            "Create", "GetBytes", "Fill", "GetInt32",
            "TickCount", "TickCount64", "GetTimestamp", "StartNew",
        };

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(WorkflowAnalyzerDescriptors.NonDeterministicApiInWorkflow);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        var memberName = memberAccess.Name.Identifier.ValueText;

        // Cheap syntactic pre-filter before the semantic lookup.
        if (!CandidateMemberNames.Contains(memberName))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        var containingType = symbol?.ContainingType?.ToDisplayString();
        if (containingType is null
            || !NonDeterministicMembers.TryGetValue((containingType, memberName), out var replacement))
        {
            return;
        }

        // Only inside a durable workflow body — a [Workflow] method, or any member of a [Workflow] type.
        if (!IsInWorkflowScope(context.ContainingSymbol))
        {
            return;
        }

        Report(
            context,
            memberAccess.GetLocation(),
            $"{symbol!.ContainingType.Name}.{memberName}",
            replacement);
    }

    /// <summary>
    /// Flags <c>new Random(...)</c>, which <see cref="AnalyzeMemberAccess"/> cannot see because construction is
    /// not a member access. A seeded instance is still non-deterministic across replay: the engine journals no
    /// draw from it, so a resumed run continues the sequence from a fresh instance.
    /// </summary>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        var typeSymbol = context.SemanticModel.GetSymbolInfo(creation.Type, context.CancellationToken).Symbol
            as INamedTypeSymbol;
        if (typeSymbol?.ToDisplayString() != RandomTypeName)
        {
            return;
        }

        if (!IsInWorkflowScope(context.ContainingSymbol))
        {
            return;
        }

        Report(
            context,
            creation.GetLocation(),
            "new Random",
            new Replacement("ctx.CallActivityAsync to generate the value in an activity", null));
    }

    private static void Report(
        SyntaxNodeAnalysisContext context,
        Location location,
        string offendingMember,
        Replacement replacement)
    {
        // Only carry a replacement the code fix can apply verbatim; see Replacement.CodeFixMethod.
        var properties = replacement.CodeFixMethod is null
            ? ImmutableDictionary<string, string?>.Empty
            : ImmutableDictionary<string, string?>.Empty
                .Add(WorkflowDiagnosticIds.ReplacementPropertyKey, replacement.CodeFixMethod);

        context.ReportDiagnostic(Diagnostic.Create(
            WorkflowAnalyzerDescriptors.NonDeterministicApiInWorkflow,
            location,
            properties,
            offendingMember,
            replacement.Guidance));
    }

    private static bool IsInWorkflowScope(ISymbol? symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingSymbol)
        {
            foreach (var attribute in current.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == WorkflowAttributeFullName)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
