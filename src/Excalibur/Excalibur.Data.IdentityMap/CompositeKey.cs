// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

namespace Excalibur.Data.IdentityMap;

/// <summary>
/// Builds deterministic composite keys for identity map lookups where a single
/// external identifier is insufficient to uniquely identify an aggregate.
/// </summary>
/// <remarks>
/// <para>
/// Use this when the external system identifies records by a combination of fields
/// (e.g., ClientNo + AccountNo, BranchCode + EmployeeId). The resulting key is
/// a pipe-delimited, ordered string that is safe for use as the <c>externalId</c>
/// parameter in <see cref="IIdentityMapStore"/> operations.
/// </para>
/// <para>
/// Names are normalized to uppercase and trimmed. Values are trimmed and have pipe
/// characters escaped to prevent ambiguity. Names must not contain the pipe (<c>|</c>)
/// or equals (<c>=</c>) characters to avoid corrupting the serialized key format.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Simple two-part key
/// var key = CompositeKey.Create("ClientNo", clientNo, "AccountNo", accountNo);
/// var accountId = await identityMap.ResolveAsync&lt;Guid&gt;("LegacyCore", key, "Account", ct);
///
/// // Three-part key
/// var key = CompositeKey.Create("Branch", branchCode, "Dept", deptCode, "EmpId", empId);
/// </code>
/// </example>
public static class CompositeKey
{
	private const char Separator = '|';
	private const char NameValueDelimiter = '=';
	private const string EscapedSeparator = "||";

	/// <summary>
	/// Creates a composite key from two named parts.
	/// </summary>
	/// <param name="name1">The first part name.</param>
	/// <param name="value1">The first part value.</param>
	/// <param name="name2">The second part name.</param>
	/// <param name="value2">The second part value.</param>
	/// <returns>A deterministic composite key string.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when any name contains the pipe (<c>|</c>) or equals (<c>=</c>) character.
	/// </exception>
	public static string Create(string name1, string value1, string name2, string value2)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name1);
		ArgumentException.ThrowIfNullOrWhiteSpace(value1);
		ArgumentException.ThrowIfNullOrWhiteSpace(name2);
		ArgumentException.ThrowIfNullOrWhiteSpace(value2);

		ValidateName(name1);
		ValidateName(name2);

		return BuildKey([(name1, value1), (name2, value2)]);
	}

	/// <summary>
	/// Creates a composite key from three named parts.
	/// </summary>
	/// <param name="name1">The first part name.</param>
	/// <param name="value1">The first part value.</param>
	/// <param name="name2">The second part name.</param>
	/// <param name="value2">The second part value.</param>
	/// <param name="name3">The third part name.</param>
	/// <param name="value3">The third part value.</param>
	/// <returns>A deterministic composite key string.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when any name contains the pipe (<c>|</c>) or equals (<c>=</c>) character.
	/// </exception>
	public static string Create(
		string name1, string value1,
		string name2, string value2,
		string name3, string value3)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name1);
		ArgumentException.ThrowIfNullOrWhiteSpace(value1);
		ArgumentException.ThrowIfNullOrWhiteSpace(name2);
		ArgumentException.ThrowIfNullOrWhiteSpace(value2);
		ArgumentException.ThrowIfNullOrWhiteSpace(name3);
		ArgumentException.ThrowIfNullOrWhiteSpace(value3);

		ValidateName(name1);
		ValidateName(name2);
		ValidateName(name3);

		return BuildKey([(name1, value1), (name2, value2), (name3, value3)]);
	}

	/// <summary>
	/// Creates a composite key from an arbitrary number of named parts.
	/// </summary>
	/// <param name="parts">The key-value parts that form the composite key.</param>
	/// <returns>A deterministic composite key string.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="parts"/> is empty, contains null/whitespace names or values,
	/// or any name contains the pipe (<c>|</c>) or equals (<c>=</c>) character.
	/// </exception>
	public static string Create(params ReadOnlySpan<(string Name, string Value)> parts)
	{
		if (parts.Length == 0)
		{
			throw new ArgumentException("At least one key part is required.", nameof(parts));
		}

		foreach (var (name, value) in parts)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(name);
			ArgumentException.ThrowIfNullOrWhiteSpace(value);
			ValidateName(name);
		}

		return BuildKey(parts);
	}

	private static string BuildKey(ReadOnlySpan<(string Name, string Value)> parts)
	{
		var sb = new StringBuilder();

		for (var i = 0; i < parts.Length; i++)
		{
			if (i > 0)
			{
				sb.Append(Separator);
			}

			var name = parts[i].Name.Trim().ToUpperInvariant();
			var value = Escape(parts[i].Value.Trim());

			sb.Append(name);
			sb.Append(NameValueDelimiter);
			sb.Append(value);
		}

		return sb.ToString();
	}

	private static string Escape(string value)
	{
		return value.Contains(Separator, StringComparison.Ordinal)
			? value.Replace("|", EscapedSeparator, StringComparison.Ordinal)
			: value;
	}

	/// <summary>
	/// Validates that a composite key part name does not contain reserved characters
	/// that would corrupt the serialized key format.
	/// </summary>
	/// <param name="name">The name to validate.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> contains the pipe (<c>|</c>) or equals (<c>=</c>) character.
	/// </exception>
	private static void ValidateName(string name)
	{
		if (name.Contains(Separator, StringComparison.Ordinal))
		{
			throw new ArgumentException(
				$"Composite key part name '{name}' must not contain the pipe character ('|') as it is used as the key separator.",
				nameof(name));
		}

		if (name.Contains(NameValueDelimiter, StringComparison.Ordinal))
		{
			throw new ArgumentException(
				$"Composite key part name '{name}' must not contain the equals character ('=') as it is used as the name-value delimiter.",
				nameof(name));
		}
	}
}
