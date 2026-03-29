// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.A3.Authorization;

namespace Excalibur.A3.Tests.A3.Authorization.Conditions;

/// <summary>
/// Spike validation tests for the Phase 4 condition expression parser and evaluator.
/// These tests validate the proof-of-concept; production tests come in implementation sprint.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public class ConditionExpressionSpikeShould
{
    private readonly ConditionExpressionEvaluator _evaluator = new();

    private static readonly Dictionary<string, string> SubjectAttrs = new()
    {
        ["Role"] = "admin",
        ["ClearanceLevel"] = "5",
        ["Department"] = "Engineering",
        ["Active"] = "true",
    };

    private static readonly Dictionary<string, string> ActionAttrs = new()
    {
        ["Name"] = "approve",
        ["Hour"] = "14",
    };

    private static readonly Dictionary<string, string> ResourceAttrs = new()
    {
        ["Amount"] = "15000",
        ["Type"] = "Order",
        ["OwnerId"] = "user-42",
        ["Region"] = "us-east-1",
    };

    // === Parser Tests ===

    [Fact]
    public void ParseSimpleStringEquality()
    {
        var node = ConditionExpressionParser.Parse("subject.Role == 'admin'");
        var comparison = node.ShouldBeOfType<ComparisonNode>();
        comparison.Property.Scope.ShouldBe(PropertyScope.Subject);
        comparison.Property.Name.ShouldBe("Role");
        comparison.Op.ShouldBe(ComparisonOp.Equal);
        comparison.Value.ShouldBeOfType<StringValue>().Value.ShouldBe("admin");
    }

    [Fact]
    public void ParseNumericGreaterThan()
    {
        var node = ConditionExpressionParser.Parse("resource.Amount > 10000");
        var comparison = node.ShouldBeOfType<ComparisonNode>();
        comparison.Property.Scope.ShouldBe(PropertyScope.Resource);
        comparison.Op.ShouldBe(ComparisonOp.GreaterThan);
        comparison.Value.ShouldBeOfType<NumberValue>().Value.ShouldBe(10000.0);
    }

    [Fact]
    public void ParseAndExpression()
    {
        var node = ConditionExpressionParser.Parse("subject.Role == 'admin' AND resource.Amount > 10000");
        var binary = node.ShouldBeOfType<BinaryNode>();
        binary.Op.ShouldBe(BinaryOp.And);
        binary.Left.ShouldBeOfType<ComparisonNode>();
        binary.Right.ShouldBeOfType<ComparisonNode>();
    }

    [Fact]
    public void ParseOrExpression()
    {
        var node = ConditionExpressionParser.Parse("subject.Role == 'admin' OR subject.Role == 'superadmin'");
        var binary = node.ShouldBeOfType<BinaryNode>();
        binary.Op.ShouldBe(BinaryOp.Or);
    }

    [Fact]
    public void ParseNotExpression()
    {
        var node = ConditionExpressionParser.Parse("NOT subject.Active == false");
        var not = node.ShouldBeOfType<NotNode>();
        not.Inner.ShouldBeOfType<ComparisonNode>();
    }

    [Fact]
    public void ParseParenthesizedExpression()
    {
        var node = ConditionExpressionParser.Parse("(subject.Role == 'admin' OR subject.Role == 'superadmin') AND resource.Amount > 10000");
        var binary = node.ShouldBeOfType<BinaryNode>();
        binary.Op.ShouldBe(BinaryOp.And);
        binary.Left.ShouldBeOfType<BinaryNode>().Op.ShouldBe(BinaryOp.Or);
    }

    [Fact]
    public void ParseNullComparison()
    {
        var node = ConditionExpressionParser.Parse("subject.ClearanceLevel != null");
        var comparison = node.ShouldBeOfType<ComparisonNode>();
        comparison.Op.ShouldBe(ComparisonOp.NotEqual);
        comparison.Value.ShouldBeOfType<NullValue>();
    }

    [Fact]
    public void ParseContainsOperator()
    {
        var node = ConditionExpressionParser.Parse("action.Name contains 'delete'");
        var comparison = node.ShouldBeOfType<ComparisonNode>();
        comparison.Op.ShouldBe(ComparisonOp.Contains);
    }

    [Fact]
    public void ParseStartsWithOperator()
    {
        var node = ConditionExpressionParser.Parse("resource.Region startsWith 'us-'");
        var comparison = node.ShouldBeOfType<ComparisonNode>();
        comparison.Op.ShouldBe(ComparisonOp.StartsWith);
    }

    [Fact]
    public void RejectMalformedExpression()
    {
        Should.Throw<FormatException>(() => ConditionExpressionParser.Parse("invalid expression"));
    }

    [Fact]
    public void RejectUnknownScope()
    {
        Should.Throw<FormatException>(() => ConditionExpressionParser.Parse("unknown.Prop == 'value'"));
    }

    [Fact]
    public void RejectUnterminatedString()
    {
        Should.Throw<FormatException>(() => ConditionExpressionParser.Parse("subject.Role == 'unterminated"));
    }

    // === Evaluator Tests ===

    [Fact]
    public void EvaluateStringEqualityTrue()
    {
        var node = ConditionExpressionParser.Parse("subject.Role == 'admin'");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateStringEqualityFalse()
    {
        var node = ConditionExpressionParser.Parse("subject.Role == 'user'");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeFalse();
    }

    [Fact]
    public void EvaluateNumericGreaterThanTrue()
    {
        var node = ConditionExpressionParser.Parse("resource.Amount > 10000");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateNumericGreaterThanFalse()
    {
        var node = ConditionExpressionParser.Parse("resource.Amount > 20000");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeFalse();
    }

    [Fact]
    public void EvaluateAndBothTrue()
    {
        var node = ConditionExpressionParser.Parse("subject.Role == 'admin' AND resource.Amount > 10000");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateAndOneFalse()
    {
        var node = ConditionExpressionParser.Parse("subject.Role == 'user' AND resource.Amount > 10000");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeFalse();
    }

    [Fact]
    public void EvaluateOrOneTrue()
    {
        var node = ConditionExpressionParser.Parse("subject.Role == 'admin' OR subject.Role == 'superadmin'");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateNotTrue()
    {
        var node = ConditionExpressionParser.Parse("NOT subject.Role == 'user'");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateNullCheckKeyExists()
    {
        var node = ConditionExpressionParser.Parse("subject.ClearanceLevel != null");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateNullCheckKeyMissing()
    {
        var node = ConditionExpressionParser.Parse("subject.NonExistent == null");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateMissingAttributeFailsClosed()
    {
        var node = ConditionExpressionParser.Parse("subject.NonExistent == 'value'");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeFalse();
    }

    [Fact]
    public void EvaluateNullDictionaryFailsClosed()
    {
        var node = ConditionExpressionParser.Parse("subject.Role == 'admin'");
        _evaluator.Evaluate(node, null, null, null).ShouldBeFalse();
    }

    [Fact]
    public void EvaluateContainsTrue()
    {
        var node = ConditionExpressionParser.Parse("action.Name contains 'approv'");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateStartsWithTrue()
    {
        var node = ConditionExpressionParser.Parse("resource.Region startsWith 'us-'");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateBooleanComparisonTrue()
    {
        var node = ConditionExpressionParser.Parse("subject.Active == true");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateComplexThreeComparison()
    {
        var node = ConditionExpressionParser.Parse(
            "subject.Role == 'admin' AND resource.Amount > 10000 AND action.Name == 'approve'");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateComplexFiveComparison()
    {
        var node = ConditionExpressionParser.Parse(
            "subject.Role == 'admin' AND resource.Amount > 10000 AND action.Name == 'approve' AND subject.Department == 'Engineering' AND resource.Type == 'Order'");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateParenthesizedLogic()
    {
        // admin can approve any amount, but regular users can only approve < 5000
        var node = ConditionExpressionParser.Parse(
            "(subject.Role == 'admin' AND resource.Amount > 10000) OR (subject.Role == 'user' AND resource.Amount < 5000)");
        _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs).ShouldBeTrue();
    }

    // === Performance Validation (Stopwatch, not BenchmarkDotNet -- spike only) ===

    [Fact]
    public void PerformanceGate_OneComparison_Under10Microseconds()
    {
        var node = ConditionExpressionParser.Parse("subject.Role == 'admin'");

        // Warm up
        for (var i = 0; i < 1000; i++)
        {
            _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs);
        }

        // Measure
        const int iterations = 100_000;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs);
        }

        sw.Stop();

        var avgMicroseconds = sw.Elapsed.TotalMicroseconds / iterations;
        avgMicroseconds.ShouldBeLessThan(10.0,
            $"1-comparison evaluation took {avgMicroseconds:F3}us avg (gate: <10us)");
    }

    [Fact]
    public void PerformanceGate_ThreeComparisons_Under10Microseconds()
    {
        var node = ConditionExpressionParser.Parse(
            "subject.Role == 'admin' AND resource.Amount > 10000 AND action.Name == 'approve'");

        // Warm up
        for (var i = 0; i < 1000; i++)
        {
            _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs);
        }

        // Measure
        const int iterations = 100_000;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs);
        }

        sw.Stop();

        var avgMicroseconds = sw.Elapsed.TotalMicroseconds / iterations;
        avgMicroseconds.ShouldBeLessThan(10.0,
            $"3-comparison evaluation took {avgMicroseconds:F3}us avg (gate: <10us)");
    }

    [Fact]
    public void PerformanceGate_FiveComparisons_Under10Microseconds()
    {
        var node = ConditionExpressionParser.Parse(
            "subject.Role == 'admin' AND resource.Amount > 10000 AND action.Name == 'approve' AND subject.Department == 'Engineering' AND resource.Type == 'Order'");

        // Warm up
        for (var i = 0; i < 1000; i++)
        {
            _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs);
        }

        // Measure
        const int iterations = 100_000;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs);
        }

        sw.Stop();

        var avgMicroseconds = sw.Elapsed.TotalMicroseconds / iterations;
        avgMicroseconds.ShouldBeLessThan(10.0,
            $"5-comparison evaluation took {avgMicroseconds:F3}us avg (gate: <10us)");
    }

    [Fact]
    public void ParseOnceEvaluateMany_ConsistentResults()
    {
        // Verify parse-once, evaluate-many pattern works correctly
        var node = ConditionExpressionParser.Parse("subject.Role == 'admin' AND resource.Amount > 10000");

        // Same node evaluated with different attribute sets
        var adminAttrs = new Dictionary<string, string> { ["Role"] = "admin" };
        var userAttrs = new Dictionary<string, string> { ["Role"] = "user" };
        var highAmount = new Dictionary<string, string> { ["Amount"] = "50000" };
        var lowAmount = new Dictionary<string, string> { ["Amount"] = "100" };

        _evaluator.Evaluate(node, adminAttrs, ActionAttrs, highAmount).ShouldBeTrue();
        _evaluator.Evaluate(node, adminAttrs, ActionAttrs, lowAmount).ShouldBeFalse();
        _evaluator.Evaluate(node, userAttrs, ActionAttrs, highAmount).ShouldBeFalse();
        _evaluator.Evaluate(node, userAttrs, ActionAttrs, lowAmount).ShouldBeFalse();
    }
}
