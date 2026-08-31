// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

namespace Tests.Shared.Helpers;

/// <summary>
/// Extracts the two halves a data request owns — the parameter names its SQL references, and the parameter
/// names it actually binds — so a lock can compare them.
/// </summary>
/// <remarks>
/// <para>
/// Promoted from <c>Excalibur.Outbox.Tests</c>, where it first caught a request that bound a tenant
/// parameter inside an <c>else if</c> branch while the SQL referenced it on every path — a defect
/// invisible to tests asserting command SHAPE (text, timeout, fields) rather than the binding/reference
/// symmetry. Shared here so every request-owning test project can apply the same lock instead of each
/// re-deriving it (and each re-deriving it slightly differently).
/// </para>
/// <para>
/// Deliberately not a regex: the scanner must be obvious enough to audit, because a lock nobody trusts gets
/// weakened the first time it goes red.
/// </para>
/// </remarks>
public static class SqlParameterTokens
{
	/// <summary>
	/// Every <c>@Name</c> token the SQL references as a PARAMETER — that is, every one the caller must bind.
	/// </summary>
	/// <remarks>
	/// T-SQL locals introduced by <c>DECLARE</c> wear the same sigil as parameters but are supplied by the
	/// batch itself, so they are excluded. Getting this wrong in the other direction is the dangerous one: if
	/// locals were counted, a legitimate multi-statement batch would report phantom unbound parameters, and
	/// the cheapest way to make that go away is to delete the arm. The exclusion is deliberately narrow —
	/// bounded to the end of the DECLARE statement — so it cannot swallow a real parameter elsewhere.
	/// </remarks>
	public static HashSet<string> ReferencedBy(string sql)
	{
		var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var locals = DeclaredLocals(sql);

		for (var i = 0; i < sql.Length; i++)
		{
			if (sql[i] != '@')
			{
				continue;
			}

			// `@@IDENTITY` and friends are engine variables, not parameters. Skip the pair outright rather
			// than letting the second '@' start a token — otherwise a future statement using one reports a
			// phantom unbound parameter, and the cheapest way to make that go away is to weaken this lock.
			if (i + 1 < sql.Length && sql[i + 1] == '@')
			{
				i++;
				while (i + 1 < sql.Length && (char.IsLetterOrDigit(sql[i + 1]) || sql[i + 1] == '_'))
				{
					i++;
				}

				continue;
			}

			var start = i + 1;
			var end = start;
			while (end < sql.Length && (char.IsLetterOrDigit(sql[end]) || sql[end] == '_'))
			{
				end++;
			}

			if (end > start && !locals.Contains(sql[start..end]))
			{
				_ = found.Add(sql[start..end]);
			}

			i = end - 1;
		}

		return found;
	}

	/// <summary>
	/// Every local the batch declares for itself. Each <c>DECLARE</c> contributes the names between the
	/// keyword and the end of that statement — the first <c>;</c> or line break, whichever comes first — so
	/// the exclusion cannot reach past the declaration into the statements that use real parameters.
	/// </summary>
	public static HashSet<string> DeclaredLocals(string sql)
	{
		var locals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var at = sql.IndexOf("DECLARE", StringComparison.OrdinalIgnoreCase);

		while (at >= 0)
		{
			var before = at == 0 || !char.IsLetterOrDigit(sql[at - 1]);
			if (before)
			{
				var stop = sql.IndexOfAny([';', '\n'], at);
				var span = stop < 0 ? sql[at..] : sql[at..stop];

				foreach (var name in ScanTokens(span))
				{
					_ = locals.Add(name);
				}
			}

			at = sql.IndexOf("DECLARE", at + "DECLARE".Length, StringComparison.OrdinalIgnoreCase);
		}

		return locals;
	}

	/// <summary>Raw <c>@Name</c> scan with no exclusions — the shared inner loop.</summary>
	private static IEnumerable<string> ScanTokens(string sql)
	{
		for (var i = 0; i < sql.Length; i++)
		{
			if (sql[i] != '@')
			{
				continue;
			}

			if (i + 1 < sql.Length && sql[i + 1] == '@')
			{
				i++;
				while (i + 1 < sql.Length && (char.IsLetterOrDigit(sql[i + 1]) || sql[i + 1] == '_'))
				{
					i++;
				}

				continue;
			}

			var start = i + 1;
			var end = start;
			while (end < sql.Length && (char.IsLetterOrDigit(sql[end]) || sql[end] == '_'))
			{
				end++;
			}

			if (end > start)
			{
				yield return sql[start..end];
			}

			i = end - 1;
		}
	}

	/// <summary>Every parameter name the command actually carries, without its sigil.</summary>
	public static HashSet<string> BoundBy(DynamicParameters parameters) =>
		new(parameters.ParameterNames.Select(static n => n.TrimStart('@')), StringComparer.OrdinalIgnoreCase);

	/// <summary>Renders a token set for a failure message.</summary>
	public static string Format(IEnumerable<string> names) =>
		string.Join(", ", names.Select(static n => "@" + n).Order(StringComparer.Ordinal));
}
