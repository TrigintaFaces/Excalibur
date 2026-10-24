// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryAnalyzers;

/// <summary>Prevents independent CLR-derived message identities in repository code.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MessageIdentityAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Identity = new(
        "REPO002", "Message identity must use its declared name", "{0}",
        "RepositoryArchitecture", DiagnosticSeverity.Error, isEnabledByDefault: true,
        description: "Message and event identities use the declared name; stores persist the name provided by the caller.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Identity);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.RepositoryMessageIdentityPolicy", out var enabled) &&
                string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                start.RegisterOperationAction(AnalyzeProperty, OperationKind.PropertyReference);
                start.RegisterOperationAction(AnalyzeDynamicMember, OperationKind.DynamicMemberReference);
                start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeProperty(OperationAnalysisContext context)
    {
        var property = (IPropertyReferenceOperation)context.Operation;
        if (!IsTypeName(property.Property.Name) ||
            !SymbolEqualityComparer.Default.Equals(property.Instance?.Type, context.Compilation.GetTypeByMetadataName("System.Type")))
        {
            return;
        }

        AnalyzeIdentityUse(context, property.Property.Name);
    }

    private static void AnalyzeDynamicMember(OperationAnalysisContext context)
    {
        var member = (IDynamicMemberReferenceOperation)context.Operation;
        if (IsTypeName(member.MemberName) && member.Instance is IDynamicInvocationOperation invocation &&
            invocation.Arguments.Length == 0 && invocation.Operation is IDynamicMemberReferenceOperation call &&
            string.Equals(call.MemberName, "GetType", StringComparison.Ordinal))
        {
            AnalyzeIdentityUse(context, member.MemberName);
        }
    }

    private static void AnalyzeIdentityUse(OperationAnalysisContext context, string memberName)
    {
        for (var operation = context.Operation.Parent; operation is not null; operation = operation.Parent)
        {
            if (operation is IInvocationOperation invocation &&
                string.Equals(invocation.TargetMethod.Name, "SetMessageType", StringComparison.Ordinal) &&
                string.Equals(invocation.TargetMethod.ContainingType.ToDisplayString(), "Excalibur.Dispatch.MessageContextExtensions", StringComparison.Ordinal))
            {
                Report(context, "Message context identity must use the declared message name.");
                return;
            }

            if ((operation is IAssignmentOperation assignment && IsIdentity(TargetName(assignment.Target))) ||
                (operation is IVariableDeclaratorOperation variable && IsIdentity(variable.Symbol.Name)) ||
                (operation is IFieldInitializerOperation field && field.InitializedFields.Any(symbol => IsIdentity(symbol.Name))) ||
                (operation is IPropertyInitializerOperation initializer && initializer.InitializedProperties.Any(symbol => IsIdentity(symbol.Name))))
            {
                Report(context, "Assign the declared message name instead of a CLR type name.");
                return;
            }
        }

        for (var symbol = context.ContainingSymbol; symbol is not null; symbol = symbol.ContainingSymbol)
        {
            if (symbol is IMethodSymbol { ReturnType.SpecialType: SpecialType.System_String } method &&
                string.Equals(method.Name, "GetTypeName", StringComparison.Ordinal) &&
                !string.Equals(memberName, "Name", StringComparison.Ordinal))
            {
                Report(context, "GetTypeName must return the declared message name.");
                return;
            }
        }
    }

    private static bool IsTypeName(string name) => string.Equals(name, "Name", StringComparison.Ordinal) ||
        string.Equals(name, "FullName", StringComparison.Ordinal) || string.Equals(name, "AssemblyQualifiedName", StringComparison.Ordinal);

    private static bool IsIdentity(string? name) => string.Equals(name, "MessageType", StringComparison.Ordinal) || string.Equals(name, "EventType", StringComparison.Ordinal);

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (string.Equals(invocation.TargetMethod.Name, "GetName", StringComparison.Ordinal) &&
            string.Equals(invocation.TargetMethod.ContainingType.ToDisplayString(), "Excalibur.Dispatch.MessageNameHelper", StringComparison.Ordinal) &&
            context.Compilation.AssemblyName?.StartsWith("Excalibur.EventSourcing.", StringComparison.Ordinal) == true)
        {
            Report(context, "Event stores must persist the supplied event name instead of deriving it again.");
        }
    }

    private static string? TargetName(IOperation target) => target switch
    {
        IPropertyReferenceOperation property => property.Property.Name,
        IFieldReferenceOperation field => field.Field.Name,
        ILocalReferenceOperation local => local.Local.Name,
        _ => null,
    };

    private static void Report(OperationAnalysisContext context, string message)
    {
        context.ReportDiagnostic(Diagnostic.Create(Identity, context.Operation.Syntax.GetLocation(), message));
    }
}
