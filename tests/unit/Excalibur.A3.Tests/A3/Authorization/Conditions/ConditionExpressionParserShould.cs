// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;

namespace Excalibur.Tests.A3.Authorization.Conditions;

/// <summary>
/// Production tests for <see cref="ConditionExpressionParser"/>.
/// Covers all operators, value types, combinators, edge cases, and error paths
/// (Sprint 727 T.13 8bhgme).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class ConditionExpressionParserShould
{
    // ──────────────────────────────────────────────
    // All comparison operators
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("==", 0)] // ComparisonOp.Equal
    [InlineData("!=", 1)] // ComparisonOp.NotEqual
    [InlineData(">", 2)]  // ComparisonOp.GreaterThan
    [InlineData("<", 3)]  // ComparisonOp.LessThan
    [InlineData(">=", 4)] // ComparisonOp.GreaterOrEqual
    [InlineData("<=", 5)] // ComparisonOp.LessOrEqual
    public void ParseAllSymbolicOperators(string opText, int expectedInt)
    {
        var expected = (ComparisonOp)expectedInt;
        var node = ConditionExpressionParser.Parse($"subject.X {opText} 42");
        var comparison = node.ShouldBeOfType<ComparisonNode>();
        comparison.Op.ShouldBe(expected);
    }

    [Fact]
    public void ParseContainsOperator()
    {
        var node = ConditionExpressionParser.Parse("resource.Tag contains 'urgent'");
        node.ShouldBeOfType<ComparisonNode>().Op.ShouldBe(ComparisonOp.Contains);
    }

    [Fact]
    public void ParseStartsWithOperator()
    {
        var node = ConditionExpressionParser.Parse("action.Name startsWith 'admin'");
        node.ShouldBeOfType<ComparisonNode>().Op.ShouldBe(ComparisonOp.StartsWith);
    }

    // ──────────────────────────────────────────────
    // All value types
    // ──────────────────────────────────────────────

    [Fact]
    public void ParseStringLiteralValue()
    {
        var node = ConditionExpressionParser.Parse("subject.Name == 'alice'");
        var c = node.ShouldBeOfType<ComparisonNode>();
        c.Value.ShouldBeOfType<StringValue>().Value.ShouldBe("alice");
    }

    [Fact]
    public void ParseEmptyStringLiteral()
    {
        var node = ConditionExpressionParser.Parse("subject.Name == ''");
        var c = node.ShouldBeOfType<ComparisonNode>();
        c.Value.ShouldBeOfType<StringValue>().Value.ShouldBe("");
    }

    [Fact]
    public void ParseIntegerNumber()
    {
        var node = ConditionExpressionParser.Parse("resource.Count == 100");
        node.ShouldBeOfType<ComparisonNode>()
            .Value.ShouldBeOfType<NumberValue>().Value.ShouldBe(100.0);
    }

    [Fact]
    public void ParseDecimalNumber()
    {
        var node = ConditionExpressionParser.Parse("resource.Price > 99.95");
        node.ShouldBeOfType<ComparisonNode>()
            .Value.ShouldBeOfType<NumberValue>().Value.ShouldBe(99.95);
    }

    [Fact]
    public void ParseNegativeNumber()
    {
        var node = ConditionExpressionParser.Parse("resource.Temp > -40");
        var c = node.ShouldBeOfType<ComparisonNode>();
        c.Value.ShouldBeOfType<NumberValue>().Value.ShouldBe(-40.0);
    }

    [Fact]
    public void ParseNegativeDecimalNumber()
    {
        var node = ConditionExpressionParser.Parse("resource.Balance >= -99.5");
        node.ShouldBeOfType<ComparisonNode>()
            .Value.ShouldBeOfType<NumberValue>().Value.ShouldBe(-99.5);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ParseBooleanValue(string boolText, bool expected)
    {
        var node = ConditionExpressionParser.Parse($"subject.Active == {boolText}");
        node.ShouldBeOfType<ComparisonNode>()
            .Value.ShouldBeOfType<BoolValue>().Value.ShouldBe(expected);
    }

    [Fact]
    public void ParseNullValue()
    {
        var node = ConditionExpressionParser.Parse("subject.Tag == null");
        node.ShouldBeOfType<ComparisonNode>().Value.ShouldBeOfType<NullValue>();
    }

    // ──────────────────────────────────────────────
    // All property scopes
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("subject", 0)] // PropertyScope.Subject
    [InlineData("action", 1)]  // PropertyScope.Action
    [InlineData("resource", 2)] // PropertyScope.Resource
    public void ParseAllPropertyScopes(string scope, int expectedInt)
    {
        var expected = (PropertyScope)expectedInt;
        var node = ConditionExpressionParser.Parse($"{scope}.Prop == 'val'");
        node.ShouldBeOfType<ComparisonNode>().Property.Scope.ShouldBe(expected);
    }

    [Fact]
    public void ParsePropertyNameWithUnderscore()
    {
        var node = ConditionExpressionParser.Parse("subject.my_attr == 'x'");
        node.ShouldBeOfType<ComparisonNode>().Property.Name.ShouldBe("my_attr");
    }

    [Fact]
    public void ParsePropertyNameWithDigits()
    {
        var node = ConditionExpressionParser.Parse("resource.level2 == 'high'");
        node.ShouldBeOfType<ComparisonNode>().Property.Name.ShouldBe("level2");
    }

    // ──────────────────────────────────────────────
    // Combinators: AND, OR, NOT, nested parentheses
    // ──────────────────────────────────────────────

    [Fact]
    public void ParseAndChain()
    {
        var node = ConditionExpressionParser.Parse(
            "subject.A == 'x' AND subject.B == 'y' AND subject.C == 'z'");
        // Should be left-associative: ((A AND B) AND C)
        var outer = node.ShouldBeOfType<BinaryNode>();
        outer.Op.ShouldBe(BinaryOp.And);
        outer.Left.ShouldBeOfType<BinaryNode>().Op.ShouldBe(BinaryOp.And);
        outer.Right.ShouldBeOfType<ComparisonNode>();
    }

    [Fact]
    public void ParseOrChain()
    {
        var node = ConditionExpressionParser.Parse(
            "subject.A == 'x' OR subject.B == 'y' OR subject.C == 'z'");
        var outer = node.ShouldBeOfType<BinaryNode>();
        outer.Op.ShouldBe(BinaryOp.Or);
        outer.Left.ShouldBeOfType<BinaryNode>().Op.ShouldBe(BinaryOp.Or);
    }

    [Fact]
    public void ParseMixedAndOrWithCorrectPrecedence()
    {
        // AND binds tighter than OR: A OR (B AND C)
        var node = ConditionExpressionParser.Parse(
            "subject.A == 'x' OR subject.B == 'y' AND subject.C == 'z'");
        var or = node.ShouldBeOfType<BinaryNode>();
        or.Op.ShouldBe(BinaryOp.Or);
        or.Left.ShouldBeOfType<ComparisonNode>();
        or.Right.ShouldBeOfType<BinaryNode>().Op.ShouldBe(BinaryOp.And);
    }

    [Fact]
    public void ParseNestedParentheses()
    {
        var node = ConditionExpressionParser.Parse(
            "((subject.A == 'x'))");
        node.ShouldBeOfType<ComparisonNode>();
    }

    [Fact]
    public void ParseDeeplyNestedExpression()
    {
        // 5+ levels of nesting
        var node = ConditionExpressionParser.Parse(
            "(((subject.A == 'x' AND subject.B == 'y') OR subject.C == 'z') AND NOT subject.D == 'w')");
        var outerAnd = node.ShouldBeOfType<BinaryNode>();
        outerAnd.Op.ShouldBe(BinaryOp.And);
        outerAnd.Right.ShouldBeOfType<NotNode>();
    }

    [Fact]
    public void ParseNotWithParentheses()
    {
        var node = ConditionExpressionParser.Parse(
            "NOT (subject.A == 'x' OR subject.B == 'y')");
        var not = node.ShouldBeOfType<NotNode>();
        not.Inner.ShouldBeOfType<BinaryNode>().Op.ShouldBe(BinaryOp.Or);
    }

    [Fact]
    public void ParseDoubleNot()
    {
        var node = ConditionExpressionParser.Parse("NOT NOT subject.Active == true");
        var outer = node.ShouldBeOfType<NotNode>();
        outer.Inner.ShouldBeOfType<NotNode>().Inner.ShouldBeOfType<ComparisonNode>();
    }

    // ──────────────────────────────────────────────
    // Keyword boundary: "ANDROID" should not match "AND"
    // ──────────────────────────────────────────────

    [Fact]
    public void NotConfuseAndroidPropertyWithAndKeyword()
    {
        // "subject.ANDROID" is a valid property -- the parser should not confuse
        // "AND" within "ANDROID" as the AND keyword due to boundary checks
        var node = ConditionExpressionParser.Parse("subject.ANDROID == 'yes'");
        var c = node.ShouldBeOfType<ComparisonNode>();
        c.Property.Name.ShouldBe("ANDROID");
    }

    // ──────────────────────────────────────────────
    // Whitespace handling
    // ──────────────────────────────────────────────

    [Fact]
    public void HandleExtraWhitespace()
    {
        var node = ConditionExpressionParser.Parse(
            "  subject.Role   ==   'admin'  AND  resource.Amount  >  100  ");
        node.ShouldBeOfType<BinaryNode>();
    }

    [Fact]
    public void HandleMinimalWhitespace()
    {
        // Operators need no whitespace around them for symbolic ops
        var node = ConditionExpressionParser.Parse("resource.X>5");
        node.ShouldBeOfType<ComparisonNode>();
    }

    // ──────────────────────────────────────────────
    // Error cases
    // ──────────────────────────────────────────────

    [Fact]
    public void ThrowOnNullInput()
    {
        Should.Throw<ArgumentNullException>(
            () => ConditionExpressionParser.Parse(null!));
    }

    [Fact]
    public void ThrowOnEmptyExpression()
    {
        Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse(""));
    }

    [Fact]
    public void ThrowOnWhitespaceOnlyExpression()
    {
        Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("   "));
    }

    [Fact]
    public void ThrowOnUnknownScope()
    {
        var ex = Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("context.Foo == 'bar'"));
        ex.Message.ShouldContain("context");
    }

    [Fact]
    public void ThrowOnMissingDotAfterScope()
    {
        Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("subject Role == 'admin'"));
    }

    [Fact]
    public void ThrowOnMissingOperator()
    {
        Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("subject.Role 'admin'"));
    }

    [Fact]
    public void ThrowOnMissingValue()
    {
        Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("subject.Role =="));
    }

    [Fact]
    public void ThrowOnUnterminatedString()
    {
        var ex = Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("subject.Role == 'unterminated"));
        ex.Message.ShouldContain("Unterminated");
    }

    [Fact]
    public void ThrowOnTrailingTokens()
    {
        var ex = Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("subject.Role == 'admin' extra"));
        ex.Message.ShouldContain("Unexpected");
    }

    [Fact]
    public void ThrowOnMissingClosingParen()
    {
        Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("(subject.Role == 'admin'"));
    }

    [Fact]
    public void ThrowOnUnknownValueKeyword()
    {
        Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("subject.Role == maybe"));
    }

    [Fact]
    public void ThrowOnInvalidNumber()
    {
        // Just a minus sign with nothing after it
        Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("resource.X > -"));
    }

    [Fact]
    public void ThrowOnIncompleteAndExpression()
    {
        Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("subject.A == 'x' AND"));
    }

    [Fact]
    public void ThrowOnIncompleteOrExpression()
    {
        Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("subject.A == 'x' OR"));
    }

    [Fact]
    public void ThrowOnIncompleteNotExpression()
    {
        Should.Throw<FormatException>(
            () => ConditionExpressionParser.Parse("NOT"));
    }

    // ──────────────────────────────────────────────
    // Long / complex expressions (stress)
    // ──────────────────────────────────────────────

    [Fact]
    public void ParseLongExpressionWithManyComparisons()
    {
        // 10 comparisons chained with AND
        var parts = Enumerable.Range(0, 10)
            .Select(i => $"subject.Attr{i} == '{i}'");
        var expr = string.Join(" AND ", parts);

        var node = ConditionExpressionParser.Parse(expr);
        node.ShouldNotBeNull();
    }

    [Fact]
    public void ParseStringLiteralWithSpaces()
    {
        var node = ConditionExpressionParser.Parse("subject.Name == 'John Doe'");
        node.ShouldBeOfType<ComparisonNode>()
            .Value.ShouldBeOfType<StringValue>().Value.ShouldBe("John Doe");
    }
}
