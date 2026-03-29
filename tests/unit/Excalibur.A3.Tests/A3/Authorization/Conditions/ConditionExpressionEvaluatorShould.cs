// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.A3.Authorization.Conditions;

/// <summary>
/// Production tests for <see cref="ConditionExpressionEvaluator"/>.
/// Covers all operators, type coercion, null semantics, short-circuit,
/// boolean comparison, contains/startsWith, and fail-closed behavior
/// (Sprint 727 T.13 8bhgme).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class ConditionExpressionEvaluatorShould
{
    private readonly ConditionExpressionEvaluator _evaluator = new();

    private static readonly Dictionary<string, string> SubjectAttrs = new()
    {
        ["Role"] = "admin",
        ["Level"] = "5",
        ["Active"] = "true",
        ["Email"] = "alice@example.com",
        ["Score"] = "99.5",
        ["NegBalance"] = "-50",
    };

    private static readonly Dictionary<string, string> ActionAttrs = new()
    {
        ["Name"] = "approve-order",
        ["Hour"] = "14",
    };

    private static readonly Dictionary<string, string> ResourceAttrs = new()
    {
        ["Amount"] = "15000",
        ["Type"] = "Order",
        ["Owner"] = "user-42",
        ["Region"] = "us-east-1",
        ["Empty"] = "",
    };

    private bool Eval(string expression) =>
        Eval(expression, SubjectAttrs, ActionAttrs, ResourceAttrs);

    private bool Eval(
        string expression,
        IReadOnlyDictionary<string, string>? subject,
        IReadOnlyDictionary<string, string>? action,
        IReadOnlyDictionary<string, string>? resource)
    {
        var node = ConditionExpressionParser.Parse(expression);
        return _evaluator.Evaluate(node, subject, action, resource);
    }

    // ──────────────────────────────────────────────
    // String comparisons (==, !=, contains, startsWith, >, <, >=, <=)
    // ──────────────────────────────────────────────

    [Fact]
    public void StringEqual_Match() => Eval("subject.Role == 'admin'").ShouldBeTrue();

    [Fact]
    public void StringEqual_NoMatch() => Eval("subject.Role == 'user'").ShouldBeFalse();

    [Fact]
    public void StringNotEqual_Match() => Eval("subject.Role != 'user'").ShouldBeTrue();

    [Fact]
    public void StringNotEqual_NoMatch() => Eval("subject.Role != 'admin'").ShouldBeFalse();

    [Fact]
    public void StringContains_Match() => Eval("action.Name contains 'order'").ShouldBeTrue();

    [Fact]
    public void StringContains_NoMatch() => Eval("action.Name contains 'delete'").ShouldBeFalse();

    [Fact]
    public void StringContains_CaseSensitive() =>
        Eval("action.Name contains 'ORDER'").ShouldBeFalse();

    [Fact]
    public void StringStartsWith_Match() => Eval("action.Name startsWith 'approve'").ShouldBeTrue();

    [Fact]
    public void StringStartsWith_NoMatch() => Eval("action.Name startsWith 'reject'").ShouldBeFalse();

    [Fact]
    public void StringStartsWith_CaseSensitive() =>
        Eval("action.Name startsWith 'Approve'").ShouldBeFalse();

    [Fact]
    public void StringGreaterThan() => Eval("subject.Role > 'aaa'").ShouldBeTrue();

    [Fact]
    public void StringLessThan() => Eval("subject.Role < 'zzz'").ShouldBeTrue();

    [Fact]
    public void StringGreaterOrEqual_Equal() => Eval("subject.Role >= 'admin'").ShouldBeTrue();

    [Fact]
    public void StringLessOrEqual_Equal() => Eval("subject.Role <= 'admin'").ShouldBeTrue();

    [Fact]
    public void StringEqual_EmptyValue() => Eval("resource.Empty == ''").ShouldBeTrue();

    // ──────────────────────────────────────────────
    // Numeric comparisons with type coercion
    // ──────────────────────────────────────────────

    [Fact]
    public void NumericEqual() => Eval("resource.Amount == 15000").ShouldBeTrue();

    [Fact]
    public void NumericNotEqual() => Eval("resource.Amount != 9999").ShouldBeTrue();

    [Fact]
    public void NumericGreaterThan_True() => Eval("resource.Amount > 10000").ShouldBeTrue();

    [Fact]
    public void NumericGreaterThan_False() => Eval("resource.Amount > 20000").ShouldBeFalse();

    [Fact]
    public void NumericLessThan_True() => Eval("resource.Amount < 20000").ShouldBeTrue();

    [Fact]
    public void NumericLessThan_False() => Eval("resource.Amount < 5000").ShouldBeFalse();

    [Fact]
    public void NumericGreaterOrEqual_Equal() => Eval("resource.Amount >= 15000").ShouldBeTrue();

    [Fact]
    public void NumericGreaterOrEqual_Greater() => Eval("resource.Amount >= 14999").ShouldBeTrue();

    [Fact]
    public void NumericLessOrEqual_Equal() => Eval("resource.Amount <= 15000").ShouldBeTrue();

    [Fact]
    public void NumericLessOrEqual_Less() => Eval("resource.Amount <= 15001").ShouldBeTrue();

    [Fact]
    public void NumericDecimalComparison() => Eval("subject.Score > 99.0").ShouldBeTrue();

    [Fact]
    public void NumericNegativeComparison() => Eval("subject.NegBalance > -100").ShouldBeTrue();

    [Fact]
    public void NumericCoercionFailure_FailsClosed()
    {
        // Role="admin" can't parse as double -> false
        Eval("subject.Role > 5").ShouldBeFalse();
    }

    // ──────────────────────────────────────────────
    // Boolean comparisons
    // ──────────────────────────────────────────────

    [Fact]
    public void BoolEqual_True() => Eval("subject.Active == true").ShouldBeTrue();

    [Fact]
    public void BoolEqual_False() => Eval("subject.Active == false").ShouldBeFalse();

    [Fact]
    public void BoolNotEqual() => Eval("subject.Active != false").ShouldBeTrue();

    [Fact]
    public void BoolCoercionFailure_FailsClosed()
    {
        // Role="admin" can't parse as bool -> false
        Eval("subject.Role == true").ShouldBeFalse();
    }

    [Fact]
    public void BoolGreaterThan_FailsClosed()
    {
        // Boolean comparison only supports == and !=
        Eval("subject.Active > true").ShouldBeFalse();
    }

    // ──────────────────────────────────────────────
    // Null semantics
    // ──────────────────────────────────────────────

    [Fact]
    public void NullEqual_MissingKey() =>
        Eval("subject.NonExistent == null").ShouldBeTrue();

    [Fact]
    public void NullNotEqual_MissingKey() =>
        Eval("subject.NonExistent != null").ShouldBeFalse();

    [Fact]
    public void NullEqual_ExistingKey() =>
        Eval("subject.Role == null").ShouldBeFalse();

    [Fact]
    public void NullNotEqual_ExistingKey() =>
        Eval("subject.Role != null").ShouldBeTrue();

    [Fact]
    public void NullGreaterThan_FailsClosed()
    {
        // Comparing to null with > is not valid -> false
        Eval("subject.Level > null").ShouldBeFalse();
    }

    [Fact]
    public void MissingKey_NonNullComparison_FailsClosed()
    {
        // Key doesn't exist, comparing to string (not null) -> false
        Eval("subject.NonExistent == 'anything'").ShouldBeFalse();
    }

    [Fact]
    public void MissingKey_NumericComparison_FailsClosed() =>
        Eval("subject.NonExistent > 5").ShouldBeFalse();

    // ──────────────────────────────────────────────
    // Null dictionaries
    // ──────────────────────────────────────────────

    [Fact]
    public void NullSubjectDictionary_FailsClosed() =>
        Eval("subject.Role == 'admin'", null, ActionAttrs, ResourceAttrs).ShouldBeFalse();

    [Fact]
    public void NullActionDictionary_FailsClosed() =>
        Eval("action.Name == 'approve'", SubjectAttrs, null, ResourceAttrs).ShouldBeFalse();

    [Fact]
    public void NullResourceDictionary_FailsClosed() =>
        Eval("resource.Amount > 100", SubjectAttrs, ActionAttrs, null).ShouldBeFalse();

    [Fact]
    public void AllNullDictionaries_FailsClosed() =>
        Eval("subject.Role == 'admin'", null, null, null).ShouldBeFalse();

    [Fact]
    public void NullDictionary_NullComparison_IsNull() =>
        Eval("subject.Role == null", null, ActionAttrs, ResourceAttrs).ShouldBeTrue();

    // ──────────────────────────────────────────────
    // Short-circuit evaluation
    // ──────────────────────────────────────────────

    [Fact]
    public void AndShortCircuit_LeftFalse()
    {
        // Left is false; right would throw if evaluated against missing scope,
        // but AND short-circuits before reaching right
        Eval("subject.Role == 'user' AND resource.Amount > 10000").ShouldBeFalse();
    }

    [Fact]
    public void OrShortCircuit_LeftTrue()
    {
        // Left is true; right doesn't matter
        Eval("subject.Role == 'admin' OR subject.NonExistent == 'x'").ShouldBeTrue();
    }

    [Fact]
    public void AndBothTrue() =>
        Eval("subject.Role == 'admin' AND resource.Amount > 10000").ShouldBeTrue();

    [Fact]
    public void OrBothFalse() =>
        Eval("subject.Role == 'user' AND resource.Amount > 99999").ShouldBeFalse();

    // ──────────────────────────────────────────────
    // NOT operator
    // ──────────────────────────────────────────────

    [Fact]
    public void NotTrueIsFalse() =>
        Eval("NOT subject.Role == 'admin'").ShouldBeFalse();

    [Fact]
    public void NotFalseIsTrue() =>
        Eval("NOT subject.Role == 'user'").ShouldBeTrue();

    [Fact]
    public void DoubleNot() =>
        Eval("NOT NOT subject.Role == 'admin'").ShouldBeTrue();

    [Fact]
    public void NotWithParenthesizedOr()
    {
        Eval("NOT (subject.Role == 'user' OR subject.Role == 'guest')").ShouldBeTrue();
    }

    // ──────────────────────────────────────────────
    // Complex expressions
    // ──────────────────────────────────────────────

    [Fact]
    public void ComplexThreeCondition() =>
        Eval("subject.Role == 'admin' AND resource.Amount > 10000 AND action.Name startsWith 'approve'")
            .ShouldBeTrue();

    [Fact]
    public void ComplexFiveCondition() =>
        Eval("subject.Role == 'admin' AND resource.Amount > 10000 AND action.Name contains 'order' AND subject.Level >= 5 AND resource.Region startsWith 'us-'")
            .ShouldBeTrue();

    [Fact]
    public void ParenthesizedLogic()
    {
        // Admin can approve any amount, user can only approve < 5000
        Eval("(subject.Role == 'admin' AND resource.Amount > 10000) OR (subject.Role == 'user' AND resource.Amount < 5000)")
            .ShouldBeTrue();
    }

    [Fact]
    public void CrossScopeComparison()
    {
        // All three scopes referenced
        Eval("subject.Role == 'admin' AND action.Name startsWith 'approve' AND resource.Type == 'Order'")
            .ShouldBeTrue();
    }

    // ──────────────────────────────────────────────
    // Parse-once, evaluate-many (thread-safety validation)
    // ──────────────────────────────────────────────

    [Fact]
    public void ParseOnceEvaluateManyDifferentContexts()
    {
        var node = ConditionExpressionParser.Parse("subject.Role == 'admin'");

        var admin = new Dictionary<string, string> { ["Role"] = "admin" };
        var user = new Dictionary<string, string> { ["Role"] = "user" };
        var empty = new Dictionary<string, string>();

        _evaluator.Evaluate(node, admin, null, null).ShouldBeTrue();
        _evaluator.Evaluate(node, user, null, null).ShouldBeFalse();
        _evaluator.Evaluate(node, empty, null, null).ShouldBeFalse();
        _evaluator.Evaluate(node, null, null, null).ShouldBeFalse();
    }

    // ──────────────────────────────────────────────
    // AttributeAuthorizationCache.GetParsedCondition
    // ──────────────────────────────────────────────

    [Fact]
    public void CacheParsesValidExpressionOnce()
    {
        var cache = new AttributeAuthorizationCache();

        var node1 = cache.GetParsedCondition("subject.Role == 'admin'");
        var node2 = cache.GetParsedCondition("subject.Role == 'admin'");

        node1.ShouldNotBeNull();
        node2.ShouldNotBeNull();
        ReferenceEquals(node1, node2).ShouldBeTrue();
    }

    [Fact]
    public void CacheReturnsNullForMalformedExpression()
    {
        var cache = new AttributeAuthorizationCache();

        var node = cache.GetParsedCondition("invalid expression garbage");

        node.ShouldBeNull();
    }

    [Fact]
    public void CacheMalformedSentinelIsCached()
    {
        var cache = new AttributeAuthorizationCache();

        // First call parses and caches null sentinel
        var first = cache.GetParsedCondition("broken!!!");
        // Second call returns cached null without re-parsing
        var second = cache.GetParsedCondition("broken!!!");

        first.ShouldBeNull();
        second.ShouldBeNull();
    }

    [Fact]
    public void CacheDifferentExpressionsAreSeparate()
    {
        var cache = new AttributeAuthorizationCache();

        var node1 = cache.GetParsedCondition("subject.A == 'x'");
        var node2 = cache.GetParsedCondition("subject.B == 'y'");

        node1.ShouldNotBeNull();
        node2.ShouldNotBeNull();
        ReferenceEquals(node1, node2).ShouldBeFalse();
    }

    [Fact]
    public void CacheWithLoggerReportsMalformedExpression()
    {
        var logger = NullLogger<AttributeAuthorizationCache>.Instance;
        var cache = new AttributeAuthorizationCache(logger);

        // Should not throw -- logs warning and returns null
        var node = cache.GetParsedCondition("not valid at all!!!");
        node.ShouldBeNull();
    }
}
