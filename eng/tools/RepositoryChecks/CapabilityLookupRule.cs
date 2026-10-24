// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Requires runtime capability validation to receive the exact provider, requested type and raw lookup result.</summary>
internal static class CapabilityLookupRule
{
    /// <summary>Checks a lookup against the validator symbol anchored to the published kit assembly.</summary>
    internal static bool IsChecked(IInvocationOperation lookup, IMethodSymbol validator, bool resolver)
    {
        IOperation raw = lookup;
        while (raw.Parent is IConversionOperation { IsImplicit: true } conversion && (conversion.Conversion.IsIdentity || conversion.Conversion.IsReference))
        {
            raw = conversion;
        }

        if (raw.Parent is not IArgumentOperation { Parameter.Ordinal: 1 } argument ||
            argument.Parent is not IInvocationOperation validation ||
            !SymbolEqualityComparer.Default.Equals(validation.TargetMethod.OriginalDefinition, validator.OriginalDefinition) ||
            validation.TargetMethod.TypeArguments.Length != 1)
        {
            return false;
        }

        var provider = resolver ? lookup.Arguments.FirstOrDefault()?.Value : lookup.Instance;
        var requested = lookup.TargetMethod.ReturnType;
        if (!resolver)
        {
            requested = (Unwrap(lookup.Arguments.FirstOrDefault()?.Value) as ITypeOfOperation)?.TypeOperand;
        }

        var validatedProvider = validation.Arguments.SingleOrDefault(item => item.Parameter?.Ordinal == 0)?.Value;
        return requested is not null && SymbolEqualityComparer.Default.Equals(requested, validation.TargetMethod.TypeArguments[0]) &&
            SameProvider(provider, validatedProvider);
    }

    private static IOperation? Unwrap(IOperation? operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion && (conversion.Conversion.IsIdentity || conversion.Conversion.IsReference))
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static bool SameProvider(IOperation? left, IOperation? right) => (Unwrap(left), Unwrap(right)) switch
    {
        (ILocalReferenceOperation a, ILocalReferenceOperation b) => SymbolEqualityComparer.Default.Equals(a.Local, b.Local),
        (IParameterReferenceOperation a, IParameterReferenceOperation b) => SymbolEqualityComparer.Default.Equals(a.Parameter, b.Parameter),
        (IInstanceReferenceOperation a, IInstanceReferenceOperation b) => a.ReferenceKind == b.ReferenceKind,
        (IFieldReferenceOperation a, IFieldReferenceOperation b) => SymbolEqualityComparer.Default.Equals(a.Field, b.Field) &&
            (a.Field.IsStatic || SameProvider(a.Instance, b.Instance)),
        _ => false,
    };
}
