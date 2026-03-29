// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Recursive descent parser for condition expressions.
/// Parses expressions like <c>subject.Role == 'admin' AND resource.Amount > 10000</c>.
/// </summary>
/// <remarks>
/// <para>Grammar:</para>
/// <code>
/// expression   := or_expr
/// or_expr      := and_expr ("OR" and_expr)*
/// and_expr     := not_expr ("AND" not_expr)*
/// not_expr     := "NOT" not_expr | comparison | "(" expression ")"
/// comparison   := property operator value
/// property     := ("subject" | "action" | "resource") "." identifier
/// identifier   := [a-zA-Z_][a-zA-Z0-9_]*
/// operator     := "==" | "!=" | ">" | "&lt;" | ">=" | "&lt;=" | "contains" | "startsWith"
/// value        := string_literal | number | "true" | "false" | "null"
/// string_literal := "'" [^']* "'"
/// number       := "-"? [0-9]+ ("." [0-9]+)?
/// </code>
/// </remarks>
internal static class ConditionExpressionParser
{
    /// <summary>
    /// Parses the expression string into an AST.
    /// </summary>
    /// <param name="expression">The condition expression to parse.</param>
    /// <returns>The root <see cref="ExpressionNode"/> of the parsed AST.</returns>
    /// <exception cref="FormatException">Thrown when the expression is malformed.</exception>
    internal static ExpressionNode Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var tokenizer = new Tokenizer(expression);
        var result = ParseOrExpression(ref tokenizer);

        if (tokenizer.HasMore)
        {
            throw new FormatException(
                $"Unexpected token '{tokenizer.PeekText()}' at position {tokenizer.Position}.");
        }

        return result;
    }

    private static ExpressionNode ParseOrExpression(ref Tokenizer tokenizer)
    {
        var left = ParseAndExpression(ref tokenizer);

        while (tokenizer.TryMatchKeyword("OR"))
        {
            var right = ParseAndExpression(ref tokenizer);
            left = new BinaryNode(left, BinaryOp.Or, right);
        }

        return left;
    }

    private static ExpressionNode ParseAndExpression(ref Tokenizer tokenizer)
    {
        var left = ParseNotExpression(ref tokenizer);

        while (tokenizer.TryMatchKeyword("AND"))
        {
            var right = ParseNotExpression(ref tokenizer);
            left = new BinaryNode(left, BinaryOp.And, right);
        }

        return left;
    }

    private static ExpressionNode ParseNotExpression(ref Tokenizer tokenizer)
    {
        if (tokenizer.TryMatchKeyword("NOT"))
        {
            var inner = ParseNotExpression(ref tokenizer);
            return new NotNode(inner);
        }

        if (tokenizer.TryMatchChar('('))
        {
            var inner = ParseOrExpression(ref tokenizer);

            if (!tokenizer.TryMatchChar(')'))
            {
                throw new FormatException(
                    $"Expected ')' at position {tokenizer.Position}.");
            }

            return inner;
        }

        return ParseComparison(ref tokenizer);
    }

    private static ComparisonNode ParseComparison(ref Tokenizer tokenizer)
    {
        var property = ParseProperty(ref tokenizer);
        var op = ParseOperator(ref tokenizer);
        var value = ParseValue(ref tokenizer);

        return new ComparisonNode(property, op, value);
    }

    private static PropertyRef ParseProperty(ref Tokenizer tokenizer)
    {
        var scopeText = tokenizer.ReadIdentifier();
        var scope = scopeText switch
        {
            "subject" => PropertyScope.Subject,
            "action" => PropertyScope.Action,
            "resource" => PropertyScope.Resource,
            _ => throw new FormatException(
                $"Expected 'subject', 'action', or 'resource' but got '{scopeText}' at position {tokenizer.Position}.")
        };

        if (!tokenizer.TryMatchChar('.'))
        {
            throw new FormatException(
                $"Expected '.' after scope '{scopeText}' at position {tokenizer.Position}.");
        }

        var name = tokenizer.ReadIdentifier();

        return new PropertyRef(scope, name);
    }

    private static ComparisonOp ParseOperator(ref Tokenizer tokenizer)
    {
        tokenizer.SkipWhitespace();

        if (tokenizer.TryMatchKeyword("contains"))
        {
            return ComparisonOp.Contains;
        }

        if (tokenizer.TryMatchKeyword("startsWith"))
        {
            return ComparisonOp.StartsWith;
        }

        var op = tokenizer.ReadOperator();
        return op switch
        {
            "==" => ComparisonOp.Equal,
            "!=" => ComparisonOp.NotEqual,
            ">=" => ComparisonOp.GreaterOrEqual,
            "<=" => ComparisonOp.LessOrEqual,
            ">" => ComparisonOp.GreaterThan,
            "<" => ComparisonOp.LessThan,
            _ => throw new FormatException(
                $"Unknown operator '{op}' at position {tokenizer.Position}.")
        };
    }

    private static ConditionValue ParseValue(ref Tokenizer tokenizer)
    {
        tokenizer.SkipWhitespace();

        // String literal
        if (tokenizer.PeekChar() == '\'')
        {
            var str = tokenizer.ReadStringLiteral();
            return new StringValue(str);
        }

        // Number (starts with digit or negative sign)
        if (tokenizer.PeekChar() is (>= '0' and <= '9') or '-')
        {
            var num = tokenizer.ReadNumber();
            return new NumberValue(num);
        }

        // Keywords: true, false, null
        var ident = tokenizer.ReadIdentifier();
        return ident switch
        {
            "true" => new BoolValue(true),
            "false" => new BoolValue(false),
            "null" => new NullValue(),
            _ => throw new FormatException(
                $"Expected a value but got '{ident}' at position {tokenizer.Position}.")
        };
    }

    /// <summary>
    /// Lightweight tokenizer that operates over a <see cref="ReadOnlySpan{T}"/> of the input string.
    /// Tracks position for error reporting.
    /// </summary>
    internal ref struct Tokenizer
    {
        private readonly string _source;
        private int _pos;

        internal Tokenizer(string source)
        {
            _source = source;
            _pos = 0;
        }

        internal int Position => _pos;
        internal bool HasMore => _pos < _source.Length && !IsOnlyWhitespaceRemaining();

        internal void SkipWhitespace()
        {
            while (_pos < _source.Length && char.IsWhiteSpace(_source[_pos]))
            {
                _pos++;
            }
        }

        internal char PeekChar()
        {
            SkipWhitespace();
            if (_pos >= _source.Length)
            {
                throw new FormatException($"Unexpected end of expression at position {_pos}.");
            }

            return _source[_pos];
        }

        internal string PeekText()
        {
            SkipWhitespace();
            var end = _pos;
            while (end < _source.Length && !char.IsWhiteSpace(_source[end]))
            {
                end++;
            }

            return _source[_pos..end];
        }

        internal bool TryMatchChar(char c)
        {
            SkipWhitespace();
            if (_pos < _source.Length && _source[_pos] == c)
            {
                _pos++;
                return true;
            }

            return false;
        }

        internal bool TryMatchKeyword(string keyword)
        {
            SkipWhitespace();
            if (_pos + keyword.Length > _source.Length)
            {
                return false;
            }

            if (!_source.AsSpan(_pos, keyword.Length).SequenceEqual(keyword.AsSpan()))
            {
                return false;
            }

            // Ensure the keyword is followed by a non-identifier character (or end of input)
            var afterKeyword = _pos + keyword.Length;
            if (afterKeyword < _source.Length && IsIdentChar(_source[afterKeyword]))
            {
                return false;
            }

            _pos = afterKeyword;
            return true;
        }

        internal string ReadIdentifier()
        {
            SkipWhitespace();
            var start = _pos;

            if (_pos >= _source.Length || !IsIdentStartChar(_source[_pos]))
            {
                throw new FormatException(
                    $"Expected identifier at position {_pos}.");
            }

            while (_pos < _source.Length && IsIdentChar(_source[_pos]))
            {
                _pos++;
            }

            return _source[start.._pos];
        }

        internal string ReadOperator()
        {
            SkipWhitespace();
            var start = _pos;

            if (_pos >= _source.Length)
            {
                throw new FormatException($"Expected operator at position {_pos}.");
            }

            var c = _source[_pos];

            // Two-character operators: ==, !=, >=, <=
            if (_pos + 1 < _source.Length)
            {
                var next = _source[_pos + 1];
                if ((c == '=' && next == '=') ||
                    (c == '!' && next == '=') ||
                    (c == '>' && next == '=') ||
                    (c == '<' && next == '='))
                {
                    _pos += 2;
                    return _source[start.._pos];
                }
            }

            // Single-character operators: >, <
            if (c is '>' or '<')
            {
                _pos++;
                return _source[start.._pos];
            }

            throw new FormatException(
                $"Expected operator at position {_pos}, got '{c}'.");
        }

        internal string ReadStringLiteral()
        {
            SkipWhitespace();
            if (_pos >= _source.Length || _source[_pos] != '\'')
            {
                throw new FormatException(
                    $"Expected string literal at position {_pos}.");
            }

            _pos++; // skip opening quote
            var start = _pos;

            while (_pos < _source.Length && _source[_pos] != '\'')
            {
                _pos++;
            }

            if (_pos >= _source.Length)
            {
                throw new FormatException(
                    $"Unterminated string literal starting at position {start - 1}.");
            }

            var value = _source[start.._pos];
            _pos++; // skip closing quote
            return value;
        }

        internal double ReadNumber()
        {
            SkipWhitespace();
            var start = _pos;

            // Optional negative sign
            if (_pos < _source.Length && _source[_pos] == '-')
            {
                _pos++;
            }

            while (_pos < _source.Length && (_source[_pos] is >= '0' and <= '9' || _source[_pos] == '.'))
            {
                _pos++;
            }

            var span = _source.AsSpan(start, _pos - start);
            if (!double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                throw new FormatException(
                    $"Invalid number '{_source[start.._pos]}' at position {start}.");
            }

            return result;
        }

        private readonly bool IsOnlyWhitespaceRemaining()
        {
            for (var i = _pos; i < _source.Length; i++)
            {
                if (!char.IsWhiteSpace(_source[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsIdentStartChar(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_';
        private static bool IsIdentChar(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';
    }
}
