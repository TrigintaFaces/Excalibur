// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Evaluates a parsed condition expression AST against authorization context attribute dictionaries.
/// Thread-safe: all state is passed via parameters; no mutable instance fields.
/// </summary>
internal sealed class ConditionExpressionEvaluator
{
    /// <summary>
    /// Evaluates the given expression node against the provided attribute dictionaries.
    /// </summary>
    /// <param name="node">The parsed expression AST root.</param>
    /// <param name="subjectAttributes">Subject attributes (roles, claims, etc.).</param>
    /// <param name="actionAttributes">Action attributes (metadata).</param>
    /// <param name="resourceAttributes">Resource attributes (owner, labels, amounts).</param>
    /// <returns><see langword="true"/> if the condition is satisfied; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Evaluate(
        ExpressionNode node,
        IReadOnlyDictionary<string, string>? subjectAttributes,
        IReadOnlyDictionary<string, string>? actionAttributes,
        IReadOnlyDictionary<string, string>? resourceAttributes)
    {
        return node switch
        {
            ComparisonNode comparison => EvaluateComparison(
                comparison, subjectAttributes, actionAttributes, resourceAttributes),
            BinaryNode binary => EvaluateBinary(
                binary, subjectAttributes, actionAttributes, resourceAttributes),
            NotNode not => !Evaluate(
                not.Inner, subjectAttributes, actionAttributes, resourceAttributes),
            _ => false, // Unknown node type -> fail-closed
        };
    }

    private bool EvaluateBinary(
        BinaryNode node,
        IReadOnlyDictionary<string, string>? subjectAttributes,
        IReadOnlyDictionary<string, string>? actionAttributes,
        IReadOnlyDictionary<string, string>? resourceAttributes)
    {
        var left = Evaluate(node.Left, subjectAttributes, actionAttributes, resourceAttributes);

        return node.Op switch
        {
            // Short-circuit: AND returns false if left is false
            BinaryOp.And => left && Evaluate(node.Right, subjectAttributes, actionAttributes, resourceAttributes),
            // Short-circuit: OR returns true if left is true
            BinaryOp.Or => left || Evaluate(node.Right, subjectAttributes, actionAttributes, resourceAttributes),
            _ => false,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EvaluateComparison(
        ComparisonNode node,
        IReadOnlyDictionary<string, string>? subjectAttributes,
        IReadOnlyDictionary<string, string>? actionAttributes,
        IReadOnlyDictionary<string, string>? resourceAttributes)
    {
        var dict = node.Property.Scope switch
        {
            PropertyScope.Subject => subjectAttributes,
            PropertyScope.Action => actionAttributes,
            PropertyScope.Resource => resourceAttributes,
            _ => null,
        };

        var rawValue = ResolveProperty(dict, node.Property.Name);

        // Null checks: attribute key missing or null
        if (node.Value is NullValue)
        {
            return node.Op switch
            {
                ComparisonOp.Equal => rawValue is null,
                ComparisonOp.NotEqual => rawValue is not null,
                _ => false, // Other operators against null -> fail-closed
            };
        }

        // If the attribute is missing and we're not comparing to null, fail-closed
        if (rawValue is null)
        {
            return false;
        }

        return node.Value switch
        {
            StringValue sv => EvaluateStringComparison(rawValue, node.Op, sv.Value),
            NumberValue nv => EvaluateNumericComparison(rawValue, node.Op, nv.Value),
            BoolValue bv => EvaluateBoolComparison(rawValue, node.Op, bv.Value),
            _ => false,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? ResolveProperty(IReadOnlyDictionary<string, string>? dict, string key)
    {
        if (dict is null)
        {
            return null;
        }

        return dict.TryGetValue(key, out var value) ? value : null;
    }

    private static bool EvaluateStringComparison(string left, ComparisonOp op, string right)
    {
        return op switch
        {
            ComparisonOp.Equal => string.Equals(left, right, StringComparison.Ordinal),
            ComparisonOp.NotEqual => !string.Equals(left, right, StringComparison.Ordinal),
            ComparisonOp.Contains => left.Contains(right, StringComparison.Ordinal),
            ComparisonOp.StartsWith => left.StartsWith(right, StringComparison.Ordinal),
            ComparisonOp.GreaterThan => string.Compare(left, right, StringComparison.Ordinal) > 0,
            ComparisonOp.LessThan => string.Compare(left, right, StringComparison.Ordinal) < 0,
            ComparisonOp.GreaterOrEqual => string.Compare(left, right, StringComparison.Ordinal) >= 0,
            ComparisonOp.LessOrEqual => string.Compare(left, right, StringComparison.Ordinal) <= 0,
            _ => false,
        };
    }

    private static bool EvaluateNumericComparison(string rawLeft, ComparisonOp op, double right)
    {
        if (!double.TryParse(rawLeft, NumberStyles.Float, CultureInfo.InvariantCulture, out var left))
        {
            return false; // Non-numeric attribute value -> fail-closed
        }

        return op switch
        {
            ComparisonOp.Equal => left == right,
            ComparisonOp.NotEqual => left != right,
            ComparisonOp.GreaterThan => left > right,
            ComparisonOp.LessThan => left < right,
            ComparisonOp.GreaterOrEqual => left >= right,
            ComparisonOp.LessOrEqual => left <= right,
            _ => false,
        };
    }

    private static bool EvaluateBoolComparison(string rawLeft, ComparisonOp op, bool right)
    {
        if (!bool.TryParse(rawLeft, out var left))
        {
            return false; // Non-boolean attribute value -> fail-closed
        }

        return op switch
        {
            ComparisonOp.Equal => left == right,
            ComparisonOp.NotEqual => left != right,
            _ => false,
        };
    }
}
