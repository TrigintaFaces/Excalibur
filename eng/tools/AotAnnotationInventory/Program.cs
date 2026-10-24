using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static AotAnnotationInventory.AnnotationSyntax;

namespace AotAnnotationInventory;

/// <summary>Builds a fail-closed inventory of explicit AOT annotations.</summary>
internal sealed class Program
{
	private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

	private readonly List<Entry> entries = new List<Entry>();
	private readonly List<Finding> findings = new List<Finding>();
	private readonly List<string> defines = new List<string>();
	private string? source;
	private string? basePath;
	private string? manifest;
	private string? manifestHash;
	private string? configuration;
	private string? targetFramework;
	private string? project;
	private string? sourcePopulationScope;
	private string? zeroDeclarationDisposition;
	private string? langVersion;
	private LanguageVersion parsedLanguageVersion;
	private JsonElement config;

	private static int Main(string[] args) => new Program().Run(args);

	private static void CheckAncestors(string path)
	{
		for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
		{
			if ((directory.Attributes & FileAttributes.ReparsePoint) != FileAttributes.None)
			{
				throw new IOException($"Refusing linked source/base ancestor: {directory.FullName}");
			}
		}
	}

	private static IEnumerable<string> Enumerate(string directory)
	{
		foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
		{
			if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != FileAttributes.None)
			{
				throw new IOException($"Refusing linked source file: {file}");
			}

			yield return file;
		}

		foreach (var child in Directory.EnumerateDirectories(directory))
		{
			if (new[] { "bin", "obj" }.Contains(Path.GetFileName(child), StringComparer.Ordinal))
			{
				continue;
			}

			if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != FileAttributes.None)
			{
				throw new IOException($"Refusing linked source directory: {child}");
			}

			foreach (var file in Enumerate(child))
			{
				yield return file;
			}
		}
	}

	private int Run(string[] args)
	{
		try
		{
			ParseArguments(args);
			ReadConfiguration();
			var files = ReadPopulation();
			Scan(files, ReadTrees(files));
		}
		catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or IOException or UnauthorizedAccessException
			or InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException or System.Security.SecurityException)
		{
			findings.Add(new("REFUSE", ex.Message, null, null, null));
		}

		return WriteResult();
	}

	private void ParseArguments(string[] args)
	{
		for (var i = 0; i < args.Length; i++)
		{
			switch (args[i])
			{
				case "--source":
					source = args[++i];
					break;
				case "--base":
					basePath = args[++i];
					break;
				case "--configuration-manifest":
					manifest = args[++i];
					break;
				default:
					throw new InvalidDataException($"Unknown option: {args[i]}");
			}
		}

		if (source is null || basePath is null || manifest is null)
		{
			throw new InvalidDataException("Required: --source <directory> --base <directory> --configuration-manifest <json>");
		}
	}

	private void ReadConfiguration()
	{
		var manifestBytes = File.ReadAllBytes(manifest!);
		manifestHash = Convert.ToHexString(SHA256.HashData(manifestBytes));
		using var configurationDocument = JsonDocument.Parse(manifestBytes);
		config = configurationDocument.RootElement.Clone();
		configuration = config.GetProperty("configuration").GetString();
		targetFramework = config.GetProperty("targetFramework").GetString();
		project = config.GetProperty("project").GetString();
		langVersion = config.GetProperty("langVersion").GetString();
		if (string.IsNullOrWhiteSpace(langVersion) || !LanguageVersionFacts.TryParse(langVersion, out parsedLanguageVersion))
		{
			throw new InvalidDataException("An explicit valid evaluated langVersion is required.");
		}

		if (string.IsNullOrWhiteSpace(configuration) || string.IsNullOrWhiteSpace(targetFramework) || string.IsNullOrWhiteSpace(project))
		{
			throw new InvalidDataException("Configuration, targetFramework and project must be explicit nonempty strings.");
		}

		foreach (var symbol in config.GetProperty("symbols").EnumerateArray())
		{
			var value = symbol.GetString();
			if (string.IsNullOrWhiteSpace(value) || !SyntaxFacts.IsValidIdentifier(value))
			{
				throw new InvalidDataException("Every preprocessor symbol must be a valid nonempty identifier.");
			}

			defines.Add(value);
		}
	}

	private string[] ReadPopulation()
	{
		source = Path.GetFullPath(source!);
		basePath = Path.GetFullPath(basePath!);
		if (!Directory.Exists(source))
		{
			throw new DirectoryNotFoundException(source);
		}

		CheckAncestors(source);
		CheckAncestors(basePath);

		// Project evaluation, not a recursive repo scan, defines the selected compilation population.
		// Enumeration remains available only as an explicit fixture population mode.
		var populationMode = config.GetProperty("populationMode").GetString();
		sourcePopulationScope = string.Equals(populationMode, "fixtureDirectory", StringComparison.Ordinal) ? "Explicit fixture directory only" :
			config.GetProperty("sourcePopulationScope").GetString();
		if (string.IsNullOrWhiteSpace(sourcePopulationScope))
		{
			throw new InvalidDataException("Manifest must explicitly describe source population scope.");
		}

		var files = populationMode switch
		{
			"evaluatedCompileItems" => config.GetProperty("sourceFiles").EnumerateArray().Select(item =>
				Path.GetFullPath(item.GetString() ?? throw new InvalidDataException("Null Compile item"), basePath!))
				.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
			"fixtureDirectory" => [.. Enumerate(source).Order(StringComparer.Ordinal)],
			_ => throw new InvalidDataException("Explicit evaluatedCompileItems or fixtureDirectory populationMode required."),
		};
		zeroDeclarationDisposition = config.TryGetProperty("zeroDeclarationDisposition", out var zero) ? zero.GetString() : null;
		if (files.Length == 0 && !(string.Equals(populationMode, "evaluatedCompileItems", StringComparison.Ordinal) &&
string.Equals(zeroDeclarationDisposition, "evaluated-source-free", StringComparison.Ordinal)))
		{
			throw new IOException("Source population is empty without an explicit evaluated-source-free disposition.");
		}

		if (files.Length != 0 && zeroDeclarationDisposition is not null)
		{
			throw new InvalidDataException("Zero-declaration disposition conflicts with nonempty Compile items.");
		}

		return files;
	}

	private Dictionary<string, SyntaxTree> ReadTrees(string[] files)
	{
		var options = new CSharpParseOptions(parsedLanguageVersion, preprocessorSymbols: defines);
		var trees = new Dictionary<string, SyntaxTree>(StringComparer.Ordinal);
		foreach (var file in files)
		{
			var relativeFile = Path.GetRelativePath(basePath!, file).Replace('\\', '/');
			if (string.Equals(relativeFile, "..", StringComparison.Ordinal) || relativeFile.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relativeFile))
			{
				throw new IOException($"Compile item escapes base: {file}");
			}

			CheckAncestors(Path.GetDirectoryName(file)!);
			if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != FileAttributes.None)
			{
				throw new IOException($"Refusing linked Compile item: {file}");
			}

			trees.Add(file, CSharpSyntaxTree.ParseText(File.ReadAllText(file, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)), options, file));
		}

		return trees;
	}

	private void Scan(string[] files, Dictionary<string, SyntaxTree> trees)
	{
		var globalAliases = trees.Values.SelectMany(t => t.GetCompilationUnitRoot().Usings)
			.Where(u => u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) && u.Alias is not null).ToArray();
		var shadows = trees.Values.SelectMany(t => t.GetRoot().DescendantNodes())
			.Select(t => t switch
			{
				BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
				DelegateDeclarationSyntax type => type.Identifier.ValueText,
				_ => string.Empty,
			})
			.Where(n => new[] { "RequiresDynamicCode", "RequiresDynamicCodeAttribute", "RequiresUnreferencedCode", "RequiresUnreferencedCodeAttribute" }.Contains(n, StringComparer.Ordinal)).ToHashSet(StringComparer.Ordinal);
		foreach (var file in files)
		{
			ScanFile(file, trees[file], globalAliases, shadows);
		}
	}

	private void ScanFile(string file, SyntaxTree tree, UsingDirectiveSyntax[] globalAliases, HashSet<string> shadows)
	{
		var relative = Path.GetRelativePath(basePath!, file).Replace('\\', '/');
		if (string.Equals(relative, "..", StringComparison.Ordinal) || relative.StartsWith("../", StringComparison.Ordinal))
		{
			throw new IOException($"Source escapes base directory: {file}");
		}

		try
		{
			var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
			foreach (var error in errors)
			{
				var point = error.Location.GetLineSpan().StartLinePosition;
				findings.Add(new(error.Id, error.GetMessage(), relative, point.Line + 1, point.Character + 1));
			}

			if (errors.Length != 0)
			{
				return;
			}

			ScanDeclarations(tree, relative, globalAliases, shadows);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
		{
			findings.Add(new("UNREADABLE", ex.Message, relative, null, null));
		}
	}

	private void ScanDeclarations(SyntaxTree tree, string relative, UsingDirectiveSyntax[] globalAliases, HashSet<string> shadows)
	{
		var root = tree.GetCompilationUnitRoot();
		foreach (var declaration in root.DescendantNodes().Where(n => n is MemberDeclarationSyntax or AccessorDeclarationSyntax))
		{
			if (!Visible(declaration))
			{
				continue;
			}

			foreach (var attribute in Attributes(declaration).SelectMany(a => a.Attributes))
			{
				var annotation = Annotation(attribute.Name, declaration, globalAliases, shadows);
				if (annotation is null)
				{
					continue;
				}

				// Source contract: 1-based position of the attribute name, not its list's '['.
				var point = tree.GetLineSpan(attribute.Name.Span).StartLinePosition;
				var signatures = Signatures(declaration).ToArray();
				if (signatures.Length == 0)
				{
					findings.Add(new("UNSUPPORTED_DECLARATION", declaration.Kind().ToString(), relative,
						point.Line + 1, point.Character + 1));
				}

				foreach (var signature in signatures)
				{
					var name = Container(declaration) + signature;
					entries.Add(new(
						relative + "::" + name + "::" + annotation,
						relative, name, annotation, point.Line + 1, point.Character + 1));
				}
			}
		}
	}

	private int WriteResult()
	{
		Console.WriteLine(JsonSerializer.Serialize(
			new
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
				.Select(g => new
				{
					identity = g.Key,
					declaration = g.First().Declaration,
					annotation = g.First().Annotation,
					sources = g.Select(e => new { path = e.Path, line = e.Line, column = e.Column }).Distinct()
						.OrderBy(e => e.path, StringComparer.Ordinal).ThenBy(e => e.line).ThenBy(e => e.column).ToArray(),
				}).ToArray(),
				diagnostics = findings,
			}, Json));
		return findings.Count == 0 ? 0 : 2;
	}

	private sealed record Entry(string Identity, string Path, string Declaration, string Annotation,
		int Line, int Column);

	private sealed record Finding(string Code, string Message, string? Path, int? Line, int? Column);
}
