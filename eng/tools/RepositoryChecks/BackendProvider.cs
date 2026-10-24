// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;

namespace RepositoryChecks;

/// <summary>Keeps the defining source symbol as the trust anchor; display identities are only report keys.</summary>
internal sealed record BackendProvider(INamedTypeSymbol Type)
{
    /// <summary>Gets the exact ledger identity.</summary>
    internal string Identity => Type.ContainingAssembly.Identity.Name + ":" + Type.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    /// <summary>Resolves this source type in the compilation that owns a reference operation.</summary>
    internal INamedTypeSymbol? Resolve(Compilation compilation) =>
        PublishedContracts.ResolveReference(compilation, Type.ContainingAssembly)?.GetTypeByMetadataName(MetadataName());

    /// <summary>Matches only anchored source types, including substitutions originating in the consumer compilation.</summary>
    internal bool Matches(INamedTypeSymbol candidate, Compilation root, Compilation owner) =>
        SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, Resolve(owner)?.OriginalDefinition) ||
        SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, Resolve(root)?.OriginalDefinition);

    private string MetadataName()
    {
        var names = new Stack<string>();
        for (var current = Type; current is not null; current = current.ContainingType)
        {
            names.Push(current.MetadataName);
        }

        var prefix = Type.ContainingNamespace.IsGlobalNamespace ? string.Empty : Type.ContainingNamespace.ToDisplayString() + ".";
        return prefix + string.Join('+', names);
    }
}
