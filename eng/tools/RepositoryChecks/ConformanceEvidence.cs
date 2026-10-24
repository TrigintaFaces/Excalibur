// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;

namespace RepositoryChecks;

/// <summary>Accumulates measured source and contract populations without applying baseline exemptions.</summary>
internal sealed class ConformanceEvidence
{
    /// <summary>Gets static test-entry-point reference coverage for published conformance arms.</summary>
    internal List<ArmSuite> ArmSuites { get; } = [];

    /// <summary>Gets reflection guard coverage measured from the published source assembly.</summary>
    internal ConformanceGuardResult? Guards { get; private set; }

    /// <summary>Gets resolved private contract bindings.</summary>
    internal HashSet<ConformanceFork> Forks { get; } = [];

    /// <summary>Gets physical source files included in evaluated compilations.</summary>
    internal HashSet<string> SourcePaths { get; } = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>Gets active syntax variants observed in actual evaluated syntax trees.</summary>
    internal Dictionary<string, HashSet<string>> EvaluatedInheritance { get; } = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>Gets distinct published contracts reached by concrete suites.</summary>
    internal HashSet<string> PublishedKits { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the number of syntax trees examined.</summary>
    internal int Sources { get; private set; }

    /// <summary>Gets the number of concrete source types examined.</summary>
    internal int ConcreteTypes { get; private set; }

    /// <summary>Gets resolved bindings to published contracts.</summary>
    internal int PublishedDerivations { get; private set; }

    /// <summary>Accumulates one evaluated project's facts.</summary>
    /// <exception cref="InvalidOperationException">Conditional source has unevaluated syntax variants.</exception>
    internal int Add(Compilation compilation, IAssemblySymbol? published, string? testRoot = null)
    {
        var found = ConformanceForkRule.Analyze(compilation, published, testRoot);
        ArmSuites.AddRange(ConformanceArmWiring.Analyze(compilation, published));
        if (published is not null && Guards is null)
        {
            Guards = ConformanceGuardRule.Analyze(compilation, published);
        }

        Forks.UnionWith(found);
        Sources += compilation.SyntaxTrees.Count();
        foreach (var tree in compilation.SyntaxTrees.Where(tree => File.Exists(tree.FilePath)))
        {
            var path = Path.GetFullPath(tree.FilePath);
            var source = tree.GetText().ToString();
            var identity = SourceOwnership.SyntaxIdentity(tree);
            if (source.Contains('#', StringComparison.Ordinal) &&
                SourceOwnership.HasUncoveredSyntax(source, new HashSet<string>(StringComparer.Ordinal) { identity }))
            {
                throw new InvalidOperationException($"Unevaluated conditional syntax in {compilation.AssemblyName}: {path}");
            }

            SourcePaths.Add(path);
            if (!EvaluatedInheritance.TryGetValue(path, out var positions))
            {
                positions = [];
                EvaluatedInheritance.Add(path, positions);
            }

            positions.Add(identity);
        }

        foreach (var type in ConformanceForkRule.Types(compilation.Assembly.GlobalNamespace).Where(type => type.TypeKind == TypeKind.Class && !type.IsAbstract))
        {
            ConcreteTypes++;
            for (var parent = type.BaseType; parent is not null; parent = parent.BaseType)
            {
                if (parent.IsAbstract && parent.Name.Contains("Conformance", StringComparison.Ordinal) &&
                    SymbolEqualityComparer.Default.Equals(parent.ContainingAssembly, published))
                {
                    PublishedDerivations++;
                    PublishedKits.Add(parent.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
                }
            }
        }

        return found.Count;
    }
}
