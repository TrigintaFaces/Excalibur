// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Regex-based implementation of <see cref="IDataMasker"/> for PII/PHI data masking.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses compiled regex patterns for
/// efficient, thread-safe masking of sensitive data patterns.
/// </para>
/// <para>
/// Performance target: PERF-004 requires less than 2% overhead on message processing.
/// This is achieved through pre-compiled regex patterns and avoiding allocations
/// where possible.
/// </para>
/// </remarks>
public sealed class RegexDataMasker : IDataMasker
{
	private static readonly RegexOptions SafeRegexOptions =
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

	private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

	// Pre-compiled regex patterns for performance
	private static readonly Regex EmailPattern = new(
		@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
		SafeRegexOptions,
		RegexTimeout);

	private static readonly Regex PhonePattern = new(
		@"\b\d{3}[-.\s]?\d{3}[-.\s]?\d{4}\b",
		SafeRegexOptions,
		RegexTimeout);

	private static readonly Regex SsnPattern = new(
		@"\b\d{3}-\d{2}-\d{4}\b",
		SafeRegexOptions,
		RegexTimeout);

	private static readonly Regex CardPattern = new(
		@"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b",
		SafeRegexOptions,
		RegexTimeout);

	private static readonly Regex IpPattern = new(
		@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",
		SafeRegexOptions,
		RegexTimeout);

	private static readonly Regex DobPattern = new(
		@"\b(0?[1-9]|1[0-2])[-/](0?[1-9]|[12]\d|3[01])[-/](19|20)\d{2}\b",
		SafeRegexOptions,
		RegexTimeout);

	private readonly MaskingRules _defaultRules;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="RegexDataMasker"/> class.
	/// </summary>
	public RegexDataMasker() : this(MaskingRules.Default) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="RegexDataMasker"/> class with custom default rules.
	/// </summary>
	/// <param name="defaultRules">The default masking rules to use.</param>
	public RegexDataMasker(MaskingRules defaultRules)
	{
		_defaultRules = defaultRules ?? MaskingRules.Default;
		_jsonOptions = new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	}

	/// <inheritdoc/>
	public string Mask(string input, MaskingRules rules)
	{
		if (string.IsNullOrEmpty(input))
		{
			return input;
		}

		ArgumentNullException.ThrowIfNull(rules);

		var result = input;

		if (rules.MaskEmail)
		{
			result = MaskEmails(result, rules.MaskCharacter);
		}

		if (rules.MaskPhone)
		{
			result = MaskPhones(result, rules.MaskCharacter);
		}

		if (rules.MaskSsn)
		{
			result = MaskSsns(result, rules.MaskCharacter);
		}

		if (rules.MaskCardNumber)
		{
			result = MaskCards(result, rules.MaskCharacter);
		}

		if (rules.MaskIpAddress)
		{
			result = MaskIps(result, rules.MaskCharacter);
		}

		if (rules.MaskDateOfBirth)
		{
			result = MaskDobs(result, rules.MaskCharacter);
		}

		return result;
	}

	/// <inheritdoc/>
	public string MaskAll(string input) => Mask(input, _defaultRules);

	/// <inheritdoc/>
	[RequiresUnreferencedCode(
		"Object masking uses JSON serialization which may require preserved members for the runtime type.")]
	[RequiresDynamicCode(
		"Object masking uses JSON serialization which may require dynamic code generation.")]
	public T MaskObject<T>(T obj) where T : class
	{
		if (obj is null)
		{
			return obj!;
		}

		// Serialize to JSON, mask the string, and deserialize back
		// This approach preserves the original object structure while masking all string values
		var json = JsonSerializer.Serialize(obj, _jsonOptions);
		var maskedJson = MaskJsonValues(json, obj.GetType());
		return JsonSerializer.Deserialize<T>(maskedJson, _jsonOptions)!;
	}

	private static IEnumerable<PropertyInfo> GetMaskableProperties(Type type)
	{
		return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p =>
			{
				var personalData = p.GetCustomAttribute<PersonalDataAttribute>();
				if (personalData?.MaskInLogs == true)
				{
					return true;
				}

				var sensitive = p.GetCustomAttribute<SensitiveAttribute>();
				return sensitive?.MaskInLogs == true;
			});
	}

	private static string GetJsonPropertyName(PropertyInfo prop)
	{
		// Use camelCase by default (matching JsonNamingPolicy.CamelCase)
		var name = prop.Name;
		if (string.IsNullOrEmpty(name))
		{
			return name;
		}

		return char.ToLowerInvariant(name[0]) + name[1..];
	}

	private static string MaskEmails(string input, char maskChar)
	{
		return EmailPattern.Replace(input, match =>
		{
			var email = match.Value;
			var atIndex = email.IndexOf('@', StringComparison.Ordinal);
			if (atIndex <= 1)
			{
				return new string(maskChar, email.Length);
			}

			// Keep first char of local part, mask middle, keep @domain structure
			var local = email[..atIndex];
			var domain = email[(atIndex + 1)..];
			var dotIndex = domain.LastIndexOf('.');

			if (dotIndex <= 1)
			{
				return
					$"{local[0]}{new string(maskChar, 3)}@{new string(maskChar, 3)}.{(dotIndex > 0 ? domain[(dotIndex + 1)..] : domain)}";
			}

			var domainName = domain[..dotIndex];
			var tld = domain[(dotIndex + 1)..];
			return $"{local[0]}{new string(maskChar, 3)}@{domainName[0]}{new string(maskChar, 3)}.{tld}";
		});
	}

	private static string MaskPhones(string input, char maskChar)
	{
		return PhonePattern.Replace(input, match =>
		{
			var phone = match.Value;
			// Keep last 4 digits visible
			var digits = phone.Where(char.IsDigit).ToArray();
			if (digits.Length < 4)
			{
				return new string(maskChar, phone.Length);
			}

			var last4 = new string(digits[^4..]);
			// Preserve separators
			var result = new char[phone.Length];
			var lastDigitPos = phone.Length - 1;
			var digitsFromEnd = 0;

			for (var i = phone.Length - 1; i >= 0; i--)
			{
				if (char.IsDigit(phone[i]))
				{
					result[i] = digitsFromEnd < 4 ? phone[i] : maskChar;
					digitsFromEnd++;
				}
				else
				{
					result[i] = phone[i];
				}
			}

			return new string(result);
		});
	}

	private static string MaskSsns(string input, char maskChar)
	{
		return SsnPattern.Replace(input, match =>
		{
			// 123-45-6789 -> ***-**-6789
			var ssn = match.Value;
			var parts = ssn.Split('-');
			if (parts.Length != 3)
			{
				return new string(maskChar, ssn.Length);
			}

			return $"{new string(maskChar, 3)}-{new string(maskChar, 2)}-{parts[2]}";
		});
	}

	private static string MaskCards(string input, char maskChar)
	{
		return CardPattern.Replace(input, match =>
		{
			// 4111-1111-1111-1111 -> ****-****-****-1111
			var card = match.Value;
			var digits = card.Where(char.IsDigit).ToArray();
			if (digits.Length < 4)
			{
				return new string(maskChar, card.Length);
			}

			var last4 = new string(digits[^4..]);
			var separator = card.Contains('-', StringComparison.Ordinal)
				? '-'
				: (card.Contains(' ', StringComparison.Ordinal) ? ' ' : '\0');

			if (separator != '\0')
			{
				return
					$"{new string(maskChar, 4)}{separator}{new string(maskChar, 4)}{separator}{new string(maskChar, 4)}{separator}{last4}";
			}

			return $"{new string(maskChar, 12)}{last4}";
		});
	}

	private static string MaskIps(string input, char maskChar)
	{
		return IpPattern.Replace(input, match =>
		{
			// 192.168.1.100 -> ***.***.***.100
			var ip = match.Value;
			var octets = ip.Split('.');
			if (octets.Length != 4)
			{
				return new string(maskChar, ip.Length);
			}

			return $"{new string(maskChar, 3)}.{new string(maskChar, 3)}.{new string(maskChar, 3)}.{octets[3]}";
		});
	}

	private static string MaskDobs(string input, char maskChar)
	{
		return DobPattern.Replace(input, match =>
		{
			// 12/25/1990 -> **/**/****
			var dob = match.Value;
			var separator = dob.Contains('/', StringComparison.Ordinal) ? '/' : '-';
			return $"{new string(maskChar, 2)}{separator}{new string(maskChar, 2)}{separator}{new string(maskChar, 4)}";
		});
	}

	private string MaskJsonValues(string json, Type rootType)
	{
		// Get properties that need masking
		var maskableProperties = GetMaskableProperties(rootType);

		var result = json;
		foreach (var prop in maskableProperties)
		{
			// Create a pattern to find this property in the JSON and mask its value
			var propPattern = new Regex(
				$@"""{GetJsonPropertyName(prop)}"":\s*""([^""]+)""",
				RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
				RegexTimeout);

			result = propPattern.Replace(result, match =>
			{
				var value = match.Groups[1].Value;
				var maskedValue = MaskAll(value);
				return $@"""{GetJsonPropertyName(prop)}"":""{maskedValue}""";
			});
		}

		// Also apply general pattern masking to all string values
		result = MaskAll(result);

		return result;
	}
}
