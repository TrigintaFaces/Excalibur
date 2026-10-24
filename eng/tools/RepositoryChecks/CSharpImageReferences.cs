// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RepositoryChecks;

/// <summary>Decodes C# string expressions before the artifact gate inspects image reference text.</summary>
internal static class CSharpImageReferences
{
    /// <summary>Includes constant concatenations and escaped literals; comments are not image declarations.</summary>
    internal static IEnumerable<string> Read(string source)
    {
        foreach (var tree in CSharpConfigurations.Parse(source))
        {
            var model = CSharpCompilation.Create("Images", [tree],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]).GetSemanticModel(tree);
            foreach (var expression in tree.GetRoot().DescendantNodes().OfType<ExpressionSyntax>())
            {
                if (expression is not (LiteralExpressionSyntax or InterpolatedStringExpressionSyntax or BinaryExpressionSyntax))
                {
                    continue;
                }

                if (model.GetConstantValue(expression) is { HasValue: true, Value: string value })
                {
                    yield return value;
                }
                else if (expression is InterpolatedStringExpressionSyntax interpolation)
                {
                    yield return string.Concat(interpolation.Contents.Select(content => content is InterpolatedStringTextSyntax text
                        ? text.TextToken.ValueText : "{dynamic}"));
                }
            }
        }
    }

    /// <summary>Decodes an explicit file roster, propagating parse and I/O failures as refusals.</summary>
    internal static async Task<int> RunAsync(string roster)
    {
        try
        {
            foreach (var path in await File.ReadAllLinesAsync(roster).ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                foreach (var value in Read(await File.ReadAllTextAsync(path).ConfigureAwait(false)).Distinct(StringComparer.Ordinal).ToArray())
                {
                    await Console.Out.WriteLineAsync(value).ConfigureAwait(false);
                }
            }

            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            await Console.Error.WriteLineAsync("REFUSE: C# image extraction: " + exception.Message).ConfigureAwait(false);
            return 3;
        }
    }
}
