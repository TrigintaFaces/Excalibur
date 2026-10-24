// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Checks resolved reflection arguments on every published kit, including kits without a suite.</summary>
internal static class ConformanceGuardRule
{
    /// <summary>Requires guard enumeration to include the visibility of the kit's actual arms.</summary>
    internal static ConformanceGuardResult Analyze(Compilation compilation, IAssemblySymbol published)
    {
        var guardless = new List<string>();
        var failures = new List<string>();
        foreach (var kit in ConformanceForkRule.Types(published.GlobalNamespace).Where(type => type.IsAbstract && type.Name.EndsWith("TestKit", StringComparison.Ordinal)))
        {
            var methods = ConformanceArmWiring.Ancestors(kit, includeSelf: true).SelectMany(type => type.GetMembers().OfType<IMethodSymbol>()).ToArray();
            var arms = methods.Where(ConformanceArmWiring.IsArm).ToArray();
            if (arms.Length == 0)
            {
                continue;
            }

            var guard = methods.FirstOrDefault(method => string.Equals(method.Name, "ConformanceSuite_ShouldWireEveryArm", StringComparison.Ordinal));
            if (guard is null)
            {
                guardless.Add(kit.ToDisplayString());
                continue;
            }

            var required = BindingFlags.Instance;
            required |= arms.Any(method => method.DeclaredAccessibility == Accessibility.Public) ? BindingFlags.Public : BindingFlags.Default;
            required |= arms.Any(method => method.DeclaredAccessibility != Accessibility.Public) ? BindingFlags.NonPublic : BindingFlags.Default;
            var flags = EnumeratedFlags(compilation, guard).ToArray();
            if (flags.Length == 0 || flags.Any(value => value is null || (value.Value & required) != required))
            {
                failures.Add(kit.ToDisplayString() + " guard must enumerate " + required + "; unresolved or narrower GetMethods arguments were found.");
            }
        }

        return new ConformanceGuardResult(guardless.Order(StringComparer.Ordinal).ToArray(), failures.Order(StringComparer.Ordinal).ToArray());
    }

    private static IEnumerable<BindingFlags?> EnumeratedFlags(Compilation compilation, IMethodSymbol guard)
    {
        foreach (var declaration in guard.DeclaringSyntaxReferences)
        {
            var syntax = declaration.GetSyntax();
            var model = ConformanceArmWiring.ModelFor(compilation, syntax.SyntaxTree);
            foreach (var node in syntax.DescendantNodes())
            {
                if (model.GetOperation(node) is not IInvocationOperation invocation ||
                    !string.Equals(invocation.TargetMethod.Name, "GetMethods", StringComparison.Ordinal) ||
                    !string.Equals(invocation.TargetMethod.ContainingType.ToDisplayString(), "System.Type", StringComparison.Ordinal))
                {
                    continue;
                }

                // The guard separately enumerates public test wrappers. Only the enumeration
                // filtered by MethodBase.IsVirtual describes the kit's conformance arms.
                if (!IsArmEnumeration(model, node))
                {
                    continue;
                }

                var argument = invocation.Arguments.FirstOrDefault(value =>
                    string.Equals(value.Parameter?.Type.ToDisplayString(), "System.Reflection.BindingFlags", StringComparison.Ordinal));
                yield return argument?.Value.ConstantValue is { HasValue: true, Value: not null } constant
                    ? (BindingFlags)Convert.ToInt32(constant.Value, CultureInfo.InvariantCulture)
                    : null;
            }
        }
    }

    private static bool IsArmEnumeration(SemanticModel model, SyntaxNode enumeration) =>
        enumeration.Ancestors().OfType<InvocationExpressionSyntax>().Any(parent =>
            model.GetOperation(parent) is IInvocationOperation filter &&
            string.Equals(filter.TargetMethod.Name, "Where", StringComparison.Ordinal) &&
            string.Equals(filter.TargetMethod.ContainingType.ToDisplayString(), "System.Linq.Enumerable", StringComparison.Ordinal) &&
            parent.DescendantNodes().Any(node => model.GetOperation(node) is IPropertyReferenceOperation property &&
                string.Equals(property.Property.Name, "IsVirtual", StringComparison.Ordinal) &&
                string.Equals(property.Property.ContainingType.ToDisplayString(), "System.Reflection.MethodBase", StringComparison.Ordinal)));
}
