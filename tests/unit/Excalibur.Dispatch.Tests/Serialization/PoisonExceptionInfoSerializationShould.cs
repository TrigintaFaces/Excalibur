// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Tests for <see cref="PoisonExceptionInfo"/> serialization via <see cref="DispatchJsonContext"/>,
/// verifying the AOT-safe concrete DTO replaces anonymous types correctly.
/// Sprint 737 T.2: PoisonMessageHandler anonymous type -> concrete DTO.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
[Trait(TraitNames.Feature, TestFeatures.AOT)]
public sealed class PoisonExceptionInfoSerializationShould
{
	[Fact]
	public void SerializeBasicExceptionInfo()
	{
		var info = new PoisonExceptionInfo
		{
			Type = "System.InvalidOperationException",
			Message = "Something went wrong",
			StackTrace = "at Foo.Bar()",
		};

		var json = JsonSerializer.Serialize(info, DispatchJsonContext.Default.PoisonExceptionInfo);

		json.ShouldNotBeNullOrWhiteSpace();
		json.ShouldContain("InvalidOperationException");
		json.ShouldContain("Something went wrong");
		json.ShouldContain("at Foo.Bar()");
	}

	[Fact]
	public void RoundTripWithInnerException()
	{
		var info = new PoisonExceptionInfo
		{
			Type = "System.AggregateException",
			Message = "Outer error",
			StackTrace = "at Outer.Method()",
			InnerException = new PoisonExceptionInfo
			{
				Type = "System.ArgumentException",
				Message = "Inner error",
				StackTrace = "at Inner.Method()",
			},
		};

		var json = JsonSerializer.Serialize(info, DispatchJsonContext.Default.PoisonExceptionInfo);
		var result = JsonSerializer.Deserialize(json, DispatchJsonContext.Default.PoisonExceptionInfo);

		result.ShouldNotBeNull();
		result.Type.ShouldBe("System.AggregateException");
		result.Message.ShouldBe("Outer error");

		result.InnerException.ShouldNotBeNull();
		result.InnerException.Type.ShouldBe("System.ArgumentException");
		result.InnerException.Message.ShouldBe("Inner error");
		result.InnerException.StackTrace.ShouldBe("at Inner.Method()");
	}

	[Fact]
	public void RoundTripWithDataDictionary()
	{
		var info = new PoisonExceptionInfo
		{
			Type = "System.InvalidOperationException",
			Message = "Data test",
			Data = new Dictionary<string, string?>
			{
				["Key1"] = "Value1",
				["Key2"] = null,
				["Key3"] = "Value3",
			},
		};

		var json = JsonSerializer.Serialize(info, DispatchJsonContext.Default.PoisonExceptionInfo);
		var result = JsonSerializer.Deserialize(json, DispatchJsonContext.Default.PoisonExceptionInfo);

		result.ShouldNotBeNull();
		result.Data.ShouldNotBeNull();
		result.Data.Count.ShouldBe(3);
		result.Data["Key1"].ShouldBe("Value1");
		result.Data["Key2"].ShouldBeNull();
		result.Data["Key3"].ShouldBe("Value3");
	}

	[Fact]
	public void SerializeNullFieldsGracefully()
	{
		var info = new PoisonExceptionInfo
		{
			Type = null,
			Message = null,
			StackTrace = null,
			InnerException = null,
			Data = null,
		};

		var json = JsonSerializer.Serialize(info, DispatchJsonContext.Default.PoisonExceptionInfo);
		var result = JsonSerializer.Deserialize(json, DispatchJsonContext.Default.PoisonExceptionInfo);

		result.ShouldNotBeNull();
		result.Type.ShouldBeNull();
		result.Message.ShouldBeNull();
		result.StackTrace.ShouldBeNull();
		result.InnerException.ShouldBeNull();
		result.Data.ShouldBeNull();
	}

	[Fact]
	public void ProduceValidJsonWithExpectedPropertyNames()
	{
		var info = new PoisonExceptionInfo
		{
			Type = "TestType",
			Message = "TestMessage",
			StackTrace = "TestStack",
		};

		var json = JsonSerializer.Serialize(info, DispatchJsonContext.Default.PoisonExceptionInfo);

		using var doc = JsonDocument.Parse(json);
		doc.RootElement.GetProperty("Type").GetString().ShouldBe("TestType");
		doc.RootElement.GetProperty("Message").GetString().ShouldBe("TestMessage");
		doc.RootElement.GetProperty("StackTrace").GetString().ShouldBe("TestStack");
	}
}
