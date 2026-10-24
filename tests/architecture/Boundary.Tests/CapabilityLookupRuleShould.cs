// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class CapabilityLookupRuleShould
{
    [Theory]
    [InlineData("Validate<IFacet>(store, store.GetService(typeof(IFacet)))", true, false)]
    [InlineData("Validate<IFacet>(other, store.GetService(typeof(IFacet)))", false, false)]
    [InlineData("Validate<IOther>(store, store.GetService(typeof(IFacet)))", false, false)]
    [InlineData("Validate<IFacet>(store, store.GetService(typeof(IFacet)) as IFacet)", false, false)]
    [InlineData("store.GetService(typeof(IFacet)) as IFacet", false, false)]
    [InlineData("Validate<IFacet>(store, other.GetService(typeof(IFacet)))", false, false)]
    [InlineData("Validate<IFacet>(store, Resolve(store))", true, true)]
    [InlineData("Validate<IFacet>(other, Resolve(store))", false, true)]
    [InlineData("Validate<IOther>(store, Resolve(store))", false, true)]
    [InlineData("Validate<IFacet>(store, Resolve(store) as IFacet)", false, true)]
    public void RequireExactRawLookup(string expression, bool expected, bool resolver)
    {
        var tree = CSharpSyntaxTree.ParseText("""
            using System;
            interface IFacet { }
            interface IOther { }
            class Kit {
                static T Validate<T>(object provider, object raw) where T : class => raw as T;
                static IFacet Resolve(IServiceProvider store) => null;
                object Check(IServiceProvider store, IServiceProvider other) =>
            """ + expression + "; }");
        var compilation = CSharpCompilation.Create("CapabilityFixture", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromFile(typeof(IServiceProvider).Assembly.Location),
                MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var model = compilation.GetSemanticModel(tree);
        var lookup = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(node => model.GetOperation(node)).OfType<IInvocationOperation>()
            .Single(call => string.Equals(call.TargetMethod.Name, resolver ? "Resolve" : "GetService", StringComparison.Ordinal));
        var validator = compilation.GetTypeByMetadataName("Kit")!.GetMembers("Validate").OfType<IMethodSymbol>().Single();
        CapabilityLookupRule.IsChecked(lookup, validator, resolver).ShouldBe(expected);
    }
}
