// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Data.Serialization;

#pragma warning disable IL2026, IL3050 // RequiresUnreferencedCode, RequiresDynamicCode

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ExcaliburJsonSerializerOptionsShould
{
	[Fact]
	public void Default_IsNotNull()
	{
		ExcaliburJsonSerializerOptions.Default.ShouldNotBeNull();
	}

	[Fact]
	public void Default_UsesIndentedFormatting()
	{
		ExcaliburJsonSerializerOptions.Default.WriteIndented.ShouldBeTrue();
	}

	[Fact]
	public void Default_UsesCamelCaseNaming()
	{
		ExcaliburJsonSerializerOptions.Default.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
	}

	[Fact]
	public void Default_IgnoresNullValues()
	{
		ExcaliburJsonSerializerOptions.Default.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
	}

	[Fact]
	public void Default_ContainsEnumConverter()
	{
		ExcaliburJsonSerializerOptions.Default.Converters
			.ShouldContain(c => c is JsonStringEnumConverter);
	}

	[Fact]
	public void IgnoreStream_IsNotNull()
	{
		ExcaliburJsonSerializerOptions.IgnoreStream.ShouldNotBeNull();
	}

	[Fact]
	public void IgnoreStream_ContainsStreamConverter()
	{
		ExcaliburJsonSerializerOptions.IgnoreStream.Converters
			.ShouldContain(c => c is IgnoreStreamJsonConverter);
	}

	[Fact]
	public void IgnoreStream_InheritsDefaultSettings()
	{
		ExcaliburJsonSerializerOptions.IgnoreStream.WriteIndented.ShouldBeTrue();
		ExcaliburJsonSerializerOptions.IgnoreStream.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
	}

	[Fact]
	public void Default_ReturnsSameInstance()
	{
		var first = ExcaliburJsonSerializerOptions.Default;
		var second = ExcaliburJsonSerializerOptions.Default;
		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void IgnoreStream_ReturnsSameInstance()
	{
		var first = ExcaliburJsonSerializerOptions.IgnoreStream;
		var second = ExcaliburJsonSerializerOptions.IgnoreStream;
		first.ShouldBeSameAs(second);
	}
}
