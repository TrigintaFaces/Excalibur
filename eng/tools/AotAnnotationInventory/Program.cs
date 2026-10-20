using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Candidate only. This enumerates syntax, not semantic bindings or compiled API availability.
// The caller must supply the same preprocessor symbols as its target configuration.
return Inventory.Run(args);

internal static class Inventory
{
    private sealed record Entry(string Identity, string Path, string Declaration, string Annotation,
        int Line, int Column);
    private sealed record Finding(string Code, string Message, string? Path, int? Line, int? Column);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    internal static int Run(string[] args)
    {
        var entries = new List<Entry>();
        var findings = new List<Finding>();
        string? source = null;
        string? basePath = null;
        string? manifest = null;
        string? manifestHash = null;
        string? configuration = null;
        string? targetFramework = null;
        string? project = null;
        string? sourcePopulationScope = null;
        string? zeroDeclarationDisposition = null;
        string? langVersion = null;
        var defines = new List<string>();
        try
        {
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--source": source = args[++i]; break;
                    case "--base": basePath = args[++i]; break;
                    case "--configuration-manifest": manifest = args[++i]; break;
                    default: throw new ArgumentException($"Unknown option: {args[i]}");
                }
            }

            if (source is null || basePath is null || manifest is null)
                throw new ArgumentException("Required: --source <directory> --base <directory> --configuration-manifest <json>");
            var manifestBytes = File.ReadAllBytes(manifest);
            manifestHash = Convert.ToHexString(SHA256.HashData(manifestBytes));
            using var configurationDocument = JsonDocument.Parse(manifestBytes);
            var config = configurationDocument.RootElement;
            configuration = config.GetProperty("configuration").GetString();
            targetFramework = config.GetProperty("targetFramework").GetString();
            project = config.GetProperty("project").GetString();
            langVersion = config.GetProperty("langVersion").GetString();
            if (string.IsNullOrWhiteSpace(langVersion) || !LanguageVersionFacts.TryParse(langVersion, out var parsedLanguageVersion))
                throw new ArgumentException("An explicit valid evaluated langVersion is required.");
            if (string.IsNullOrWhiteSpace(configuration) || string.IsNullOrWhiteSpace(targetFramework) || string.IsNullOrWhiteSpace(project))
                throw new ArgumentException("Configuration, targetFramework and project must be explicit nonempty strings.");
            foreach (var symbol in config.GetProperty("symbols").EnumerateArray())
            {
                var value = symbol.GetString();
                if (string.IsNullOrWhiteSpace(value) || !SyntaxFacts.IsValidIdentifier(value))
                    throw new ArgumentException("Every preprocessor symbol must be a valid nonempty identifier.");
                defines.Add(value);
            }
            source = Path.GetFullPath(source);
            basePath = Path.GetFullPath(basePath);
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
            CheckAncestors(source);
            CheckAncestors(basePath);
            // Project evaluation, not a recursive repo scan, defines the selected compilation population.
            // Enumeration remains available only as an explicit fixture population mode.
            var populationMode = config.GetProperty("populationMode").GetString();
            sourcePopulationScope = populationMode == "fixtureDirectory" ? "Explicit fixture directory only" :
                config.GetProperty("sourcePopulationScope").GetString();
            if (string.IsNullOrWhiteSpace(sourcePopulationScope))
                throw new ArgumentException("Manifest must explicitly describe source population scope.");
            var files = populationMode switch
            {
                "evaluatedCompileItems" => config.GetProperty("sourceFiles").EnumerateArray().Select(item =>
                    Path.GetFullPath(item.GetString() ?? throw new ArgumentException("Null Compile item"), basePath))
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                "fixtureDirectory" => Enumerate(source).Order(StringComparer.Ordinal).ToArray(),
                _ => throw new ArgumentException("Explicit evaluatedCompileItems or fixtureDirectory populationMode required.")
            };
            zeroDeclarationDisposition = config.TryGetProperty("zeroDeclarationDisposition", out var zero) ? zero.GetString() : null;
            if (files.Length == 0 && !(populationMode == "evaluatedCompileItems" &&
                zeroDeclarationDisposition == "evaluated-source-free"))
                throw new IOException("Source population is empty without an explicit evaluated-source-free disposition.");
            if (files.Length != 0 && zeroDeclarationDisposition is not null)
                throw new ArgumentException("Zero-declaration disposition conflicts with nonempty Compile items.");
            var options = new CSharpParseOptions(parsedLanguageVersion, preprocessorSymbols: defines);
            var trees = new Dictionary<string, SyntaxTree>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                var relativeFile = Path.GetRelativePath(basePath, file).Replace('\\', '/');
                if (relativeFile == ".." || relativeFile.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relativeFile))
                    throw new IOException($"Compile item escapes base: {file}");
                CheckAncestors(Path.GetDirectoryName(file)!);
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"Refusing linked Compile item: {file}");
                trees.Add(file, CSharpSyntaxTree.ParseText(File.ReadAllText(file, new UTF8Encoding(false, true)), options, file));
            }
            var globalAliases = trees.Values.SelectMany(t => t.GetCompilationUnitRoot().Usings)
                .Where(u => u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) && u.Alias is not null).ToArray();
            var shadows = trees.Values.SelectMany(t => t.GetRoot().DescendantNodes())
                .Select(t => t switch { BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
                    DelegateDeclarationSyntax type => type.Identifier.ValueText, _ => "" })
                .Where(n => n is "RequiresDynamicCode" or "RequiresDynamicCodeAttribute" or
                    "RequiresUnreferencedCode" or "RequiresUnreferencedCodeAttribute").ToHashSet(StringComparer.Ordinal);
            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(basePath, file).Replace('\\', '/');
                if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal))
                    throw new IOException($"Source escapes base directory: {file}");
                try
                {
                    var tree = trees[file];
                    var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
                    foreach (var error in errors)
                    {
                        var point = error.Location.GetLineSpan().StartLinePosition;
                        findings.Add(new(error.Id, error.GetMessage(), relative, point.Line + 1, point.Character + 1));
                    }
                    if (errors.Length != 0) continue;
                    var root = tree.GetCompilationUnitRoot();
                    foreach (var declaration in root.DescendantNodes().Where(n => n is MemberDeclarationSyntax or AccessorDeclarationSyntax))
                    {
                        if (!Visible(declaration)) continue;
                        foreach (var attribute in Attributes(declaration).SelectMany(a => a.Attributes))
                        {
                            var annotation = Annotation(attribute.Name, declaration, globalAliases, shadows);
                            if (annotation is null) continue;
                            // Source contract: 1-based position of the attribute name, not its list's '['.
                            var point = tree.GetLineSpan(attribute.Name.Span).StartLinePosition;
                            var signatures = Signatures(declaration).ToArray();
                            if (signatures.Length == 0)
                                findings.Add(new("UNSUPPORTED_DECLARATION", declaration.Kind().ToString(), relative,
                                    point.Line + 1, point.Character + 1));
                            foreach (var signature in signatures)
                            {
                                var name = Container(declaration) + signature;
                                entries.Add(new(relative + "::" + name + "::" + annotation,
                                    relative, name, annotation, point.Line + 1, point.Character + 1));
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
                {
                    findings.Add(new("UNREADABLE", ex.Message, relative, null, null));
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or IOException or UnauthorizedAccessException
            or InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException or System.Security.SecurityException)
        {
            findings.Add(new("REFUSE", ex.Message, null, null, null));
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            complete = findings.Count == 0,
            scope = "Explicit declaration-level annotations only; type annotations are one entry, not expanded to members; spelling-sensitive syntax identities, not compiled API equivalence; active branches in the explicit configuration only",
            configuration,
            project,
            sourcePopulationScope,
            zeroDeclarationDisposition,
            targetFramework,
            langVersion,
            configurationManifestSha256 = manifestHash,
            preprocessorSymbols = defines.Order(StringComparer.Ordinal).ToArray(),
            entries = entries.GroupBy(e => e.Identity, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new { identity = g.Key, declaration = g.First().Declaration, annotation = g.First().Annotation,
                    sources = g.Select(e => new { path = e.Path, line = e.Line, column = e.Column }).Distinct()
                        .OrderBy(e => e.path, StringComparer.Ordinal).ThenBy(e => e.line).ThenBy(e => e.column).ToArray() }).ToArray(),
            diagnostics = findings
        }, Json));
        return findings.Count == 0 ? 0 : 2;
    }

    private static void CheckAncestors(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Refusing linked source/base ancestor: {directory.FullName}");
    }

    private static IEnumerable<string> Enumerate(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Refusing linked source file: {file}");
            yield return file;
        }
        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (Path.GetFileName(child) is "bin" or "obj") continue;
            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Refusing linked source directory: {child}");
            foreach (var file in Enumerate(child)) yield return file;
        }
    }

    private static string Text(SyntaxNode node) => string.Concat(node.DescendantTokens().Select(t => t.Text));
    private static string? Annotation(NameSyntax name, SyntaxNode context,
        IEnumerable<UsingDirectiveSyntax> globalAliases, HashSet<string> shadows)
    {
        var value = Text(name).Replace("global::", "", StringComparison.Ordinal);
        // Resolve lexical using aliases, including aliases for the attribute itself or its namespace.
        var aliases = context.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()
            .SelectMany(n => n.Usings).Concat(context.SyntaxTree.GetCompilationUnitRoot().Usings)
            .Where(u => u.Alias is not null && u.Name is not null).Concat(globalAliases).Distinct();
        foreach (var group in aliases.GroupBy(a => a.Alias!.Name.Identifier.ValueText, StringComparer.Ordinal))
        {
            var prefix = group.Key;
            if (group.Any(a => a.Name is null))
                throw new InvalidDataException($"Unsupported non-name alias {prefix}; semantic review required.");
            var targets = group.Select(a => Text(a.Name!)).Distinct(StringComparer.Ordinal).ToArray();
            if (targets.Length != 1) throw new InvalidDataException($"Conflicting alias {prefix} in {context.SyntaxTree.FilePath}");
            var target = targets[0];
            if (value == prefix) value = target;
            else if (value.StartsWith(prefix + ".", StringComparison.Ordinal) || value.StartsWith(prefix + "::", StringComparison.Ordinal))
                value = target + "." + value[(prefix.Length + (value[prefix.Length] == ':' ? 2 : 1))..];
        }
        value = value.Replace("global::", "", StringComparison.Ordinal);
        var shortName = value.StartsWith("System.Diagnostics.CodeAnalysis.", StringComparison.Ordinal)
            ? value["System.Diagnostics.CodeAnalysis.".Length..] : value;
        if (shadows.Contains(shortName) || shadows.Contains(shortName + "Attribute"))
            throw new InvalidDataException($"Attribute shadow requires semantic review: {value}");
        if (shortName.Contains("RequiresDynamicCode", StringComparison.Ordinal) || shortName.Contains("RequiresUnreferencedCode", StringComparison.Ordinal))
            if (shortName is not ("RequiresDynamicCode" or "RequiresDynamicCodeAttribute" or "RequiresUnreferencedCode" or "RequiresUnreferencedCodeAttribute"))
                throw new InvalidDataException($"Unresolved or conflicting annotation: {value}");
        return shortName switch
        {
            "RequiresDynamicCode" or "RequiresDynamicCodeAttribute" => "RequiresDynamicCode",
            "RequiresUnreferencedCode" or "RequiresUnreferencedCodeAttribute" => "RequiresUnreferencedCode",
            _ => null
        };
    }

    private static SyntaxList<AttributeListSyntax> Attributes(SyntaxNode node) => node switch
    {
        MemberDeclarationSyntax m => m.AttributeLists,
        AccessorDeclarationSyntax a => a.AttributeLists,
        _ => default
    };

    private static bool Visible(SyntaxNode node)
    {
        if (node is BaseNamespaceDeclarationSyntax) return false;
        if (node.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any(t => !OwnVisibility(t))) return false;
        if (node is AccessorDeclarationSyntax accessor)
            return accessor.Parent?.Parent is MemberDeclarationSyntax owner && Visible(owner)
                && (!accessor.Modifiers.Any() || OwnVisibility(accessor));
        return OwnVisibility(node);
    }

    private static bool OwnVisibility(SyntaxNode node)
    {
        var modifiers = node switch
        {
            MemberDeclarationSyntax m => m.Modifiers,
            AccessorDeclarationSyntax a => a.Modifiers,
            _ => default
        };
        if (modifiers.Any(SyntaxKind.PrivateKeyword) || modifiers.Any(SyntaxKind.FileKeyword)) return false;
        if (node is MethodDeclarationSyntax { ExplicitInterfaceSpecifier: not null }
            or PropertyDeclarationSyntax { ExplicitInterfaceSpecifier: not null }
            or IndexerDeclarationSyntax { ExplicitInterfaceSpecifier: not null }
            or EventDeclarationSyntax { ExplicitInterfaceSpecifier: not null }) return false;
        if (modifiers.Any(SyntaxKind.PublicKeyword) || modifiers.Any(SyntaxKind.ProtectedKeyword)) return true;
        return node.Parent is EnumDeclarationSyntax ||
            node.Parent is InterfaceDeclarationSyntax && !modifiers.Any(SyntaxKind.InternalKeyword);
    }

    private static string Arity(TypeParameterListSyntax? list) => list is null ? "" : "`" + list.Parameters.Count;
    private static string Parameters(SeparatedSyntaxList<ParameterSyntax> parameters) => "(" + string.Join(",", parameters.Select(p =>
        string.Join(" ", p.Modifiers.Select(m => m.Text).Append(p.Type is null ? "?" : Text(p.Type))))) + ")";
    private static string Container(SyntaxNode node)
    {
        var parts = node.Ancestors().Reverse().Select(n => n switch
        {
            BaseNamespaceDeclarationSyntax ns => Text(ns.Name),
            TypeDeclarationSyntax type => type.Identifier.ValueText + Arity(type.TypeParameterList),
            EnumDeclarationSyntax type => type.Identifier.ValueText,
            _ => null
        }).Where(n => n is not null);
        return string.Join(".", parts) is { Length: > 0 } value ? value + "." : "";
    }

    private static IEnumerable<string> Signatures(SyntaxNode node)
    {
        var signature = node switch
        {
            MethodDeclarationSyntax m => m.Identifier.ValueText + Arity(m.TypeParameterList) + Parameters(m.ParameterList.Parameters),
            ConstructorDeclarationSyntax c => "#ctor" + Parameters(c.ParameterList.Parameters),
            OperatorDeclarationSyntax o => "operator " + o.CheckedKeyword.Text + o.OperatorToken.Text + Parameters(o.ParameterList.Parameters),
            ConversionOperatorDeclarationSyntax c => c.ImplicitOrExplicitKeyword.Text + " operator " + c.CheckedKeyword.Text + Text(c.Type) + Parameters(c.ParameterList.Parameters),
            IndexerDeclarationSyntax i => "this" + Parameters(i.ParameterList.Parameters),
            PropertyDeclarationSyntax p => p.Identifier.ValueText,
            EventDeclarationSyntax e => e.Identifier.ValueText,
            TypeDeclarationSyntax t => t.Identifier.ValueText + Arity(t.TypeParameterList),
            EnumDeclarationSyntax e => e.Identifier.ValueText,
            EnumMemberDeclarationSyntax e => e.Identifier.ValueText,
            DelegateDeclarationSyntax d => d.Identifier.ValueText + Arity(d.TypeParameterList) + Parameters(d.ParameterList.Parameters),
            AccessorDeclarationSyntax a => Signatures(a.Parent!.Parent!).Single() + "." + a.Keyword.Text,
            _ => null
        };
        if (signature is not null) yield return signature;
        if (node is BaseFieldDeclarationSyntax field)
            foreach (var variable in field.Declaration.Variables) yield return variable.Identifier.ValueText;
    }
}
