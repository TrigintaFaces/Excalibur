// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RepositoryChecks;

/// <summary>Extracts decoded C# strings for SQL artifact checks without parsing C# in a shell script.</summary>
internal static class CSharpSqlStrings
{
    /// <summary>Preserves interpolation expressions as named SQL placeholders and excludes comments.</summary>
    /// <exception cref="InvalidOperationException">A SQL composition or record boundary cannot be evaluated safely.</exception>
    internal static string[] Read(string source, bool ddlOnly = false)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tree in CSharpConfigurations.Parse(source))
        {
            var compilation = CSharpCompilation.Create("SqlStrings", [tree],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
            var model = compilation.GetSemanticModel(tree);
            foreach (var expression in tree.GetRoot().DescendantNodes().OfType<ExpressionSyntax>())
            {
                if (expression is not (LiteralExpressionSyntax or InterpolatedStringExpressionSyntax or BinaryExpressionSyntax))
                {
                    continue;
                }

                var value = ReadExpression(expression, model);
                if (value is null && expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression)
                    && binary.DescendantNodes().OfType<ExpressionSyntax>().Select(part => ReadExpression(part, model))
                        .OfType<string>().Any(value => IsSql(value) && (!ddlOnly || IsDdl(value))))
                {
                    throw new InvalidOperationException("Unresolved SQL string concatenation: " + expression.ToFullString());
                }

                if (value is not null)
                {
                    if (value.Split('\n').Any(line => string.Equals(line.TrimEnd('\r'), "__END_CSHARP_SQL__", StringComparison.Ordinal)))
                    {
                        throw new InvalidOperationException("SQL string contains the reserved record delimiter.");
                    }

                    values.Add(value);
                }
            }
        }

        return values.ToArray();
    }

    /// <summary>Writes one file's SQL candidates with explicit boundaries, refusing incomplete syntax.</summary>
    internal static async Task<int> RunAsync(string path)
    {
        try
        {
            foreach (var value in Read(await File.ReadAllTextAsync(path).ConfigureAwait(false)))
            {
                await Console.Out.WriteLineAsync(value).ConfigureAwait(false);
                await Console.Out.WriteLineAsync("__END_CSHARP_SQL__").ConfigureAwait(false);
            }

            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            await Console.Error.WriteLineAsync("REFUSE: C# SQL extraction: " + exception.Message).ConfigureAwait(false);
            return 2;
        }
    }

    /// <summary>Decodes constants and interpolated SQL templates without executing source.</summary>
    internal static string? ReadExpression(ExpressionSyntax expression, SemanticModel model) => model.GetConstantValue(expression) is { HasValue: true, Value: string constant }
        ? constant
        : expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => ReadExpression(parenthesized.Expression, model),
        LiteralExpressionSyntax { Token.Value: string value } => value,
        InterpolatedStringExpressionSyntax interpolation => string.Concat(interpolation.Contents.Select(content => content switch
        {
            InterpolatedStringTextSyntax text => text.TextToken.ValueText,
            InterpolationSyntax hole => "{" + hole.Expression.ToFullString().Trim() + "}",
            _ => string.Empty,
        })),
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) && ReadExpression(binary.Left, model) is { } left && ReadExpression(binary.Right, model) is { } right => left + right,
        _ => null,
    };

    private static bool IsDdl(string value) => new[] { "CREATE", "ALTER" }
        .Contains(value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), StringComparer.OrdinalIgnoreCase);

    private static bool IsSql(string value) => new[] { "CREATE", "ALTER", "UPDATE", "INSERT", "DELETE", "MERGE", "SELECT", "FROM", "SET", "WITH" }
        .Contains(value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), StringComparer.OrdinalIgnoreCase);
}
