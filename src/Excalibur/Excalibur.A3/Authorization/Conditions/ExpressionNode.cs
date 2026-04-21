// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// Base type for condition expression AST nodes.
/// </summary>
internal abstract record ExpressionNode;

/// <summary>
/// A comparison between a property reference and a value (e.g., <c>subject.Role == 'admin'</c>).
/// </summary>
internal sealed record ComparisonNode(
    PropertyRef Property, ComparisonOp Op, ConditionValue Value) : ExpressionNode;

/// <summary>
/// A binary logical operation (AND / OR) combining two sub-expressions.
/// </summary>
internal sealed record BinaryNode(
    ExpressionNode Left, BinaryOp Op, ExpressionNode Right) : ExpressionNode;

/// <summary>
/// A logical NOT applied to an inner expression.
/// </summary>
internal sealed record NotNode(ExpressionNode Inner) : ExpressionNode;

/// <summary>
/// Logical operators for <see cref="BinaryNode"/>.
/// </summary>
internal enum BinaryOp
{
    And,
    Or,
}

/// <summary>
/// Comparison operators for <see cref="ComparisonNode"/>.
/// </summary>
internal enum ComparisonOp
{
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual,
    Contains,
    StartsWith,
}

/// <summary>
/// Identifies which attribute dictionary to look up and which key to read.
/// </summary>
internal readonly record struct PropertyRef(PropertyScope Scope, string Name);

/// <summary>
/// The attribute dictionary scope for a <see cref="PropertyRef"/>.
/// </summary>
internal enum PropertyScope
{
    Subject,
    Action,
    Resource,
}

/// <summary>
/// Base type for condition expression literal values.
/// </summary>
internal abstract record ConditionValue;

/// <summary>
/// A string literal value (single-quoted in the expression grammar).
/// </summary>
internal sealed record StringValue(string Value) : ConditionValue;

/// <summary>
/// A numeric literal value.
/// </summary>
internal sealed record NumberValue(double Value) : ConditionValue;

/// <summary>
/// A boolean literal value.
/// </summary>
internal sealed record BoolValue(bool Value) : ConditionValue;

/// <summary>
/// The null literal.
/// </summary>
internal sealed record NullValue() : ConditionValue;
