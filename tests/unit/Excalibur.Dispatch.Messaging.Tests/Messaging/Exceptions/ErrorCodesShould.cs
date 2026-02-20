// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
///     Tests for the <see cref="ErrorCodes" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ErrorCodesShould
{
	[Fact]
	public void HaveUniqueErrorCodes()
	{
		var fields = typeof(ErrorCodes)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
			.Select(f => (string)f.GetRawConstantValue()!)
			.ToList();

		fields.Count.ShouldBeGreaterThan(0);
		fields.Distinct(StringComparer.Ordinal).Count().ShouldBe(fields.Count, "All error codes should be unique");
	}

	[Fact]
	public void FollowCategoryPrefixPattern()
	{
		var fields = typeof(ErrorCodes)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
			.Select(f => (string)f.GetRawConstantValue()!)
			.ToList();

		foreach (var code in fields)
		{
			code.Length.ShouldBeGreaterThanOrEqualTo(4, $"Error code '{code}' should have at least 4 characters (prefix + number)");
			code[..3].ShouldMatch("[A-Z]{3}", $"Error code '{code}' should start with 3 uppercase letters");
		}
	}

	[Theory]
	[InlineData("CFG", "Configuration")]
	[InlineData("VAL", "Validation")]
	[InlineData("MSG", "Messaging")]
	[InlineData("SER", "Serialization")]
	[InlineData("NET", "Network")]
	[InlineData("SEC", "Security")]
	[InlineData("DAT", "Data")]
	[InlineData("TIM", "Timeout")]
	[InlineData("RES", "Resource")]
	[InlineData("SYS", "System")]
	public void HaveCodesForAllCategories(string prefix, string description)
	{
		_ = description;
		var fields = typeof(ErrorCodes)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
			.Select(f => (string)f.GetRawConstantValue()!)
			.ToList();

		fields.Any(c => c.StartsWith(prefix, StringComparison.Ordinal)).ShouldBeTrue(
			$"Should have at least one error code with prefix '{prefix}'");
	}

	[Fact]
	public void HaveUnknownErrorCodeDefined()
	{
		ErrorCodes.UnknownError.ShouldBe("UNK001");
	}
}
