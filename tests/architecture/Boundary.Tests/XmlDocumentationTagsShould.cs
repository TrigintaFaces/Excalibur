// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Boundary.Tests;

/// <summary>Protects the documentation contract emitted into package XML and IntelliSense.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class XmlDocumentationTagsShould(ITestOutputHelper output)
{
    internal sealed record DuplicateTag(string Tag, int Count, int Line);

    internal static IReadOnlyList<DuplicateTag> Analyze(string source)
    {
        var duplicates = new HashSet<DuplicateTag>();
        foreach (var tree in CSharpSourceInventory.ParseConfigurations(source))
        {
            var documents = tree.GetRoot().DescendantTrivia(descendIntoTrivia: true)
                .Select(trivia => trivia.GetStructure()).OfType<DocumentationCommentTriviaSyntax>();
            foreach (var document in documents)
            {
                var elements = document.DescendantNodes().Select(node => node switch
                {
                    XmlElementSyntax element => element.StartTag.Name,
                    XmlEmptyElementSyntax element => element.Name,
                    _ => null,
                }).Where(name => name is { Prefix: null }).Select(name => name!.LocalName.ValueText);
                foreach (var group in elements.Where(name => name is "summary" or "remarks" or "value")
                             .GroupBy(name => name, StringComparer.Ordinal).Where(group => group.Count() > 1))
                {
                    duplicates.Add(new DuplicateTag(group.Key, group.Count(),
                        document.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
                }
            }
        }

        return duplicates.OrderBy(item => item.Line).ThenBy(item => item.Tag, StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public void PreserveEachProductionDocumentationBlockWithoutDuplicateContractTags()
    {
        var files = CSharpSourceInventory.ReadProductionSources();
        output.WriteLine($"Examined {files.Count} source files, including committed Designer and generated source outside bin/obj.");
        files.SelectMany(file => Analyze(file.Text).Select(tag => $"{file.Path}:{tag.Line}: <{tag.Tag}> appears {tag.Count} times"))
            .ShouldBeEmpty("each documentation block may contain only one summary, remarks, and value element");
    }

    [Theory]
    [InlineData("/// <summary>one</summary>\n/// <summary>two</summary>", 1)]
    [InlineData("/// <remarks>one</remarks>\n/// <remarks>two</remarks>", 1)]
    [InlineData("/// <value>one</value>\n/// <value>two</value>", 1)]
    [InlineData("/** <summary>one</summary>\n * <summary>two</summary> */", 1)]
    [InlineData("/// <summary\n/// >one</summary>\n/// <summary>two</summary>", 1)]
    [InlineData("/// <summary/>\n/// <summary>two</summary>", 1)]
    [InlineData("/// <summary>one</summary>\n/// <remarks>details</remarks>\n/// <value>value</value>", 0)]
    [InlineData("/// <summary>one</summary>\n/// <!-- <summary>comment</summary> -->", 0)]
    [InlineData("/// <summary><![CDATA[<summary>literal</summary>]]></summary>", 0)]
    [InlineData("/// <summary>&lt;summary&gt;literal&lt;/summary&gt;</summary>", 0)]
    [InlineData("// <summary>one</summary> <summary>two</summary>", 0)]
    public void CountXmlElementsRatherThanTextMatches(string documentation, int expectedDuplicates) =>
        Analyze(documentation + "\npublic class Example { }").Count.ShouldBe(expectedDuplicates);

    [Fact]
    public void KeepSeparateMembersSeparate() => Analyze(
        "class Example {\n/// <summary>A</summary>\nint A;\n/// <summary>B</summary>\nint B;\n}").ShouldBeEmpty();

    [Fact]
    public void IgnoreDocumentationShapedRawStringContents() => Analyze(
        "class Example { string Text = \"\"\"\n/// <summary>A</summary>\n/// <summary>B</summary>\n\"\"\"; }").ShouldBeEmpty();

    [Fact]
    public void ExamineInactiveConditionalDocumentation() => Analyze(
        "#if FEATURE\n/// <summary>A</summary>\n/// <summary>B</summary>\nclass Example {}\n#endif").Count.ShouldBe(1);

    [Fact]
    public void RefuseAnEmptySourcePopulation() =>
        Should.Throw<InvalidOperationException>(() => CSharpSourceInventory.ReadSources([]));

    [Fact]
    public void RefuseAnUnreadableSourceFile() =>
        Should.Throw<FileNotFoundException>(() => CSharpSourceInventory.ReadSources(
            [Path.Combine(Path.GetTempPath(), $"missing-documentation-source-{Guid.NewGuid():N}.cs")]));

    [Theory]
    [InlineData("bin", false)]
    [InlineData("obj", false)]
    [InlineData("object", true)]
    [InlineData("Generated", true)]
    public void ExcludeOnlyBuildOutputDirectories(string directory, bool included) =>
        CSharpSourceInventory.IsProductionSource(Path.Combine("src", directory, "Example.g.cs")).ShouldBe(included);

    [Theory]
    [InlineData("Resource.Designer.cs")]
    [InlineData("Committed.Generated.cs")]
    [InlineData("Committed.g.cs")]
    public void ExamineCommittedSourceRegardlessOfItsFilename(string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"xml-doc-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, "/// <summary>A</summary>\n/// <summary>B</summary>\nclass Example {}");
            var source = CSharpSourceInventory.ReadSources([path]).Single();
            Analyze(source.Text).Count.ShouldBe(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
