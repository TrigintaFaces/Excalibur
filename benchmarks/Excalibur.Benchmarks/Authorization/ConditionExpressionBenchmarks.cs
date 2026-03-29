// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.A3.Authorization;

namespace Excalibur.Benchmarks.Authorization;

/// <summary>
/// BenchmarkDotNet benchmarks for condition expression parsing and evaluation.
/// Gate: all evaluations must complete in &lt; 10us.
/// Sprint 727 T.14 (g9dii8).
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
public class ConditionExpressionBenchmarks
{
    private readonly ConditionExpressionEvaluator _evaluator = new();

    // Pre-parsed ASTs for evaluate-only benchmarks
    private ExpressionNode _oneComparison = null!;
    private ExpressionNode _threeComparisons = null!;
    private ExpressionNode _fiveComparisons = null!;

    // Expression strings for parse benchmarks
    private const string OneComparisonExpr = "subject.Role == 'admin'";
    private const string ThreeComparisonExpr =
        "subject.Role == 'admin' AND resource.Amount > 10000 AND action.Name == 'approve'";
    private const string FiveComparisonExpr =
        "subject.Role == 'admin' AND resource.Amount > 10000 AND action.Name == 'approve' AND subject.Department == 'Engineering' AND resource.Type == 'Order'";

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

    [GlobalSetup]
    public void Setup()
    {
        _oneComparison = ConditionExpressionParser.Parse(OneComparisonExpr);
        _threeComparisons = ConditionExpressionParser.Parse(ThreeComparisonExpr);
        _fiveComparisons = ConditionExpressionParser.Parse(FiveComparisonExpr);
    }

    // ──────────────────────────────────────────────
    // Parse benchmarks (string -> AST)
    // ──────────────────────────────────────────────

    [Benchmark]
    public object Parse_1_Comparison() =>
        ConditionExpressionParser.Parse(OneComparisonExpr);

    [Benchmark]
    public object Parse_3_Comparisons() =>
        ConditionExpressionParser.Parse(ThreeComparisonExpr);

    [Benchmark]
    public object Parse_5_Comparisons() =>
        ConditionExpressionParser.Parse(FiveComparisonExpr);

    // ──────────────────────────────────────────────
    // Evaluate benchmarks (AST + dicts -> bool)
    // ──────────────────────────────────────────────

    [Benchmark]
    public bool Evaluate_1_Comparison() =>
        _evaluator.Evaluate(_oneComparison, SubjectAttrs, ActionAttrs, ResourceAttrs);

    [Benchmark]
    public bool Evaluate_3_Comparisons() =>
        _evaluator.Evaluate(_threeComparisons, SubjectAttrs, ActionAttrs, ResourceAttrs);

    [Benchmark]
    public bool Evaluate_5_Comparisons() =>
        _evaluator.Evaluate(_fiveComparisons, SubjectAttrs, ActionAttrs, ResourceAttrs);

    // ──────────────────────────────────────────────
    // Parse + Evaluate combined (end-to-end latency)
    // ──────────────────────────────────────────────

    [Benchmark]
    public bool ParseAndEvaluate_1_Comparison()
    {
        var node = ConditionExpressionParser.Parse(OneComparisonExpr);
        return _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs);
    }

    [Benchmark]
    public bool ParseAndEvaluate_3_Comparisons()
    {
        var node = ConditionExpressionParser.Parse(ThreeComparisonExpr);
        return _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs);
    }

    [Benchmark]
    public bool ParseAndEvaluate_5_Comparisons()
    {
        var node = ConditionExpressionParser.Parse(FiveComparisonExpr);
        return _evaluator.Evaluate(node, SubjectAttrs, ActionAttrs, ResourceAttrs);
    }
}
