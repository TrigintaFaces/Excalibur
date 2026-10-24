// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Measures actual Testcontainers image arguments, including compiler-resolved constants.</summary>
internal static class ContainerImageRule
{
    /// <summary>Returns every call, including unresolved arguments instead of silently omitting them.</summary>
    internal static IEnumerable<ContainerImageFact> Find(Compilation compilation, SyntaxTree tree, string path)
    {
        var model = compilation.GetSemanticModel(tree);
        foreach (var syntax in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetOperation(syntax) is not IInvocationOperation call || !string.Equals(call.TargetMethod.Name, "WithImage", StringComparison.Ordinal) ||
!string.Equals(call.TargetMethod.ContainingAssembly.Identity.Name, "Testcontainers", StringComparison.Ordinal))
            {
                continue;
            }

            var argument = call.Arguments.FirstOrDefault();
            var image = argument?.Value.ConstantValue is { HasValue: true, Value: string constant } ? constant : null;
            yield return new ContainerImageFact(path, syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1, image, Classify(image));
        }
    }

    /// <summary>Preserves concrete-tag or digest policy; missing and latest tags float.</summary>
    internal static string Classify(string? image)
    {
        if (image is null)
        {
            return "unresolved";
        }

        if (image.Contains("@sha256:", StringComparison.Ordinal))
        {
            return "digest";
        }

        var last = image[(image.LastIndexOf('/') + 1)..];
        var colon = last.LastIndexOf(':');
        return colon < 0 || colon == last.Length - 1 || last[(colon + 1)..].Contains("latest", StringComparison.OrdinalIgnoreCase) ? "floating" : "concrete";
    }
}
