// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Requires emulator client construction to use the enrolled container's endpoint and transport.</summary>
internal static class CosmosFixtureRule
{
    /// <summary>Inspects resolved constructors and fluent invocation chains in a source compilation.</summary>
    internal static CosmosFixtureResult Analyze(Compilation compilation, CancellationToken cancellationToken = default)
    {
        var builder = compilation.GetTypeByMetadataName("Testcontainers.CosmosDb.CosmosDbBuilder");
        var clientBuilder = compilation.GetTypeByMetadataName("Microsoft.Azure.Cosmos.Fluent.CosmosClientBuilder");
        var client = compilation.GetTypeByMetadataName("Microsoft.Azure.Cosmos.CosmosClient");
        var creations = Creations(compilation, cancellationToken);
        var violations = new List<CosmosFixtureViolation>();
        var fixtures = 0;
        var clients = 0;
        foreach (var group in creations.GroupBy(item => item.Owner, SymbolEqualityComparer.Default))
        {
            var containers = group.Where(item => Same(item.Creation.Type, builder)).ToArray();
            if (containers.Length == 0)
            {
                continue;
            }

            fixtures++;
            var symbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var container in containers)
            {
                var symbol = AssignmentTarget(container.Creation);
                if (symbol is null)
                {
                    violations.Add(new CosmosFixtureViolation("Container assignment cannot be resolved", container.Creation.Syntax.GetLocation()));
                }
                else
                {
                    symbols.Add(symbol);
                }
            }

            var constructions = group.Where(item => Same(item.Creation.Type, clientBuilder) || Same(item.Creation.Type, client)).ToArray();
            if (constructions.Length == 0)
            {
                violations.Add(new CosmosFixtureViolation("No SDK client construction was found", containers[0].Creation.Syntax.GetLocation()));
            }

            foreach (var (_, creation) in constructions)
            {
                clients++;
                if (!Same(creation.Type, clientBuilder) || !ValidClient(creation, symbols, clientBuilder!))
                {
                    violations.Add(new CosmosFixtureViolation("Client must use the same enrolled container for its connection string and HTTP factory", creation.Syntax.GetLocation()));
                }
            }
        }

        return new CosmosFixtureResult(fixtures, clients, violations);
    }

    private static List<(INamedTypeSymbol Owner, IObjectCreationOperation Creation)> Creations(Compilation compilation, CancellationToken cancellationToken)
    {
        var creations = new List<(INamedTypeSymbol Owner, IObjectCreationOperation Creation)>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var syntax in tree.GetRoot(cancellationToken).DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                if (model.GetOperation(syntax, cancellationToken) is IObjectCreationOperation creation &&
                    model.GetEnclosingSymbol(syntax.SpanStart, cancellationToken)?.ContainingType is { } owner)
                {
                    creations.Add((owner, creation));
                }
            }
        }

        return creations;
    }

    private static bool ValidClient(IObjectCreationOperation creation, HashSet<ISymbol> containers, INamedTypeSymbol clientBuilder)
    {
        var connection = creation.Arguments.FirstOrDefault(argument => string.Equals(argument.Parameter?.Name, "connectionString", StringComparison.Ordinal));
        if (Unwrap(connection?.Value) is not IInvocationOperation invocation ||
            !string.Equals(invocation.TargetMethod.Name, "GetConnectionString", StringComparison.Ordinal) ||
            !string.Equals(invocation.TargetMethod.ContainingType.ToDisplayString(), "Testcontainers.CosmosDb.CosmosDbContainer", StringComparison.Ordinal))
        {
            return false;
        }

        var container = ReferencedSymbol(invocation.Instance);
        if (container is null || !containers.Contains(container))
        {
            return false;
        }

        var factories = FluentChain(creation).Where(call => Same(call.TargetMethod.ContainingType, clientBuilder) &&
            string.Equals(call.TargetMethod.Name, "WithHttpClientFactory", StringComparison.Ordinal)).ToArray();
        return factories.Length == 1 && ValidFactory(factories[0], container);
    }

    private static bool ValidFactory(IInvocationOperation factory, ISymbol container)
    {
        if (factory.Arguments.Length != 1 || Unwrap(factory.Arguments[0].Value) is not IDelegateCreationOperation
            { Target: IAnonymousFunctionOperation lambda, })
        {
            return false;
        }

        var returns = lambda.Body.Descendants().OfType<IReturnOperation>().Where(item => BelongsTo(item, lambda)).ToArray();
        return returns.Length != 0 && returns.All(item => Unwrap(item.ReturnedValue) is IPropertyReferenceOperation property &&
            string.Equals(property.Property.Name, "HttpClient", StringComparison.Ordinal) &&
            string.Equals(property.Property.ContainingType.ToDisplayString(), "Testcontainers.CosmosDb.CosmosDbContainer", StringComparison.Ordinal) &&
            Same(ReferencedSymbol(property.Instance), container));
    }

    private static bool BelongsTo(IOperation operation, IAnonymousFunctionOperation lambda)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                return ReferenceEquals(current, lambda);
            }
        }

        return false;
    }

    private static IEnumerable<IInvocationOperation> FluentChain(IOperation operation)
    {
        while (operation.Parent is IInvocationOperation invocation && ReferenceEquals(invocation.Instance, operation))
        {
            yield return invocation;
            operation = invocation;
        }
    }

    private static ISymbol? AssignmentTarget(IObjectCreationOperation creation)
    {
        var last = FluentChain(creation).LastOrDefault() ?? (IOperation)creation;
        return last.Parent switch
        {
            ISimpleAssignmentOperation assignment => ReferencedSymbol(assignment.Target),
            IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declaration } => declaration.Symbol,
            IFieldInitializerOperation fields when fields.InitializedFields.Length == 1 => fields.InitializedFields[0],
            _ => null,
        };
    }

    private static IOperation? Unwrap(IOperation? operation) => operation is IConversionOperation conversion ? Unwrap(conversion.Operand) : operation;

    private static ISymbol? ReferencedSymbol(IOperation? operation) => Unwrap(operation) switch
    {
        IFieldReferenceOperation field when field.Instance is null or IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance } => field.Field,
        ILocalReferenceOperation local => local.Local,
        IParameterReferenceOperation parameter => parameter.Parameter,
        _ => null,
    };

    private static bool Same(ISymbol? left, ISymbol? right) => left is not null && right is not null && SymbolEqualityComparer.Default.Equals(left, right);
}
