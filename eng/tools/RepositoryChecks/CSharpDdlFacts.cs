// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RepositoryChecks;

/// <summary>Extracts C# facts from historical source snapshots without depending on current binaries.</summary>
internal static class CSharpDdlFacts
{
    /// <summary>Returns syntax-owned declarations and decoded SQL; unresolved expressions remain explicit.</summary>
    /// <exception cref="InvalidOperationException">Supplied snapshots have duplicate identities.</exception>
    internal static string Read(string source, IReadOnlyDictionary<string, string>? companions = null)
    {
        var properties = new HashSet<(string Owner, string Name, string? Value)>();
        var fields = new HashSet<(string Name, string Type)>();
        var comments = new HashSet<string>(StringComparer.Ordinal);
        var verifications = new HashSet<string>(StringComparer.Ordinal);
        const string RootPath = "__ddl_root__.cs";
        var snapshots = new Dictionary<string, string>(StringComparer.Ordinal) { [RootPath] = source };
        foreach (var entry in companions ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            if (!snapshots.TryAdd(entry.Key, entry.Value))
            {
                throw new InvalidOperationException("Duplicate DDL source snapshot identity: " + entry.Key);
            }
        }

        foreach (var trees in CSharpConfigurations.ParseSources(snapshots))
        {
            var tree = trees.Single(candidate => string.Equals(candidate.FilePath, RootPath, StringComparison.Ordinal));
            var compilation = CSharpCompilation.Create("HistoricalDdl", trees, [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            ReadDeclarations(root, model, properties, fields);

            foreach (var trivia in root.DescendantTrivia().Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)))
            {
                comments.Add(trivia.ToString());
            }

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(method =>
                method.Identifier.ValueText.StartsWith("Verify", StringComparison.Ordinal) && method.Identifier.ValueText.Contains("Schema", StringComparison.Ordinal)))
            {
                verifications.Add(ReadVerification(method, compilation));
            }
        }

        return JsonSerializer.Serialize(new
        {
            sql = CSharpSqlStrings.Read(source, ddlOnly: true),
            properties = properties.Select(property => new { owner = property.Owner, name = property.Name, value = property.Value }),
            fields = fields.Select(field => new { name = field.Name, type = field.Type }),
            comments,
            verifications,
        });
    }

    /// <summary>Serves JSON string snapshots over standard input; every response includes an explicit verdict.</summary>
    /// <exception cref="InvalidOperationException">The request has no source snapshot.</exception>
    internal static async Task<int> RunAsync()
    {
        while (await Console.In.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            try
            {
                using var request = JsonDocument.Parse(line);
                string? source;
                Dictionary<string, string>? companions = null;
                if (request.RootElement.ValueKind == JsonValueKind.String)
                {
                    source = request.RootElement.GetString();
                }
                else
                {
                    source = request.RootElement.GetProperty("source").GetString();
                    companions = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var entry in request.RootElement.GetProperty("companions").EnumerateObject())
                    {
                        var companionSource = entry.Value.GetString()
                            ?? throw new InvalidOperationException("Missing companion source snapshot.");
                        if (!companions.TryAdd(entry.Name, companionSource))
                        {
                            throw new InvalidOperationException("Duplicate companion source snapshot identity: " + entry.Name);
                        }
                    }
                }

                var facts = Read(source ?? throw new InvalidOperationException("Missing C# source snapshot."), companions);
                await Console.Out.WriteLineAsync(facts).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new { error = exception.Message })).ConfigureAwait(false);
            }
        }

        return 0;
    }

    private static void ReadDeclarations(SyntaxNode root, SemanticModel model,
        HashSet<(string Owner, string Name, string? Value)> properties, HashSet<(string Name, string Type)> fields)
    {
        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (model.GetDeclaredSymbol(property) is not IPropertySymbol symbol || symbol.Type.SpecialType != SpecialType.System_String)
            {
                continue;
            }

            var expression = property.Initializer?.Value ?? property.ExpressionBody?.Expression
                ?? property.AccessorList?.Accessors.FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration))?.ExpressionBody?.Expression;
            properties.Add((symbol.ContainingType.ToDisplayString(), symbol.Name,
                expression is null ? null : CSharpSqlStrings.ReadExpression(expression, model)));
        }

        foreach (var declaration in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            foreach (var variable in declaration.Declaration.Variables)
            {
                var type = model.GetTypeInfo(declaration.Declaration.Type).Type;
                fields.Add((variable.Identifier.ValueText, type?.ToDisplayString() ?? declaration.Declaration.Type.ToString()));
            }
        }
    }

    private static string ReadVerification(MethodDeclarationSyntax method, CSharpCompilation compilation)
    {
        var nodes = new Queue<SyntaxNode>();
        var visited = new HashSet<SyntaxNode>();
        var values = new HashSet<string>(StringComparer.Ordinal);
        nodes.Enqueue(method);
        while (nodes.TryDequeue(out var node))
        {
            if (!visited.Add(node))
            {
                continue;
            }

            var model = compilation.GetSemanticModel(node.SyntaxTree);
            foreach (var expression in node.DescendantNodes(child => ReferenceEquals(child, node) || child is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)).OfType<ExpressionSyntax>())
            {
                if (expression.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().Any(invocation => invocation.Expression is IdentifierNameSyntax identifier && string.Equals(identifier.Identifier.ValueText, "nameof", StringComparison.Ordinal)))
                {
                    continue;
                }

                if (CSharpSqlStrings.ReadExpression(expression, model) is { } value)
                {
                    values.Add(value);
                }

                var symbol = model.GetSymbolInfo(expression).Symbol;
                if ((symbol is IMethodSymbol && expression is InvocationExpressionSyntax) || symbol is IFieldSymbol or IPropertySymbol)
                {
                    foreach (var reference in symbol.DeclaringSyntaxReferences.Where(reference => compilation.ContainsSyntaxTree(reference.SyntaxTree)))
                    {
                        nodes.Enqueue(reference.GetSyntax());
                    }
                }
            }
        }

        return string.Join('\n', values);
    }
}
