// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Aot;

/// <summary>
/// Detailed AOT annotation tests for Kafka Schema Registry.
/// Per AD-522.1: Only Kafka SchemaRegistry uses Activator.CreateInstance via CreateSubjectNameStrategy().
/// This is the single reflection risk in all 5 transport packages.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.AOT")]
public sealed class KafkaSchemaRegistryAotShould
{
	[Fact]
	public void CreateSubjectNameStrategy_WithDefaultStrategy_ReturnWithoutReflection()
	{
		// Default strategy (TopicName enum) should not use Activator.CreateInstance
		var options = new ConfluentSchemaRegistryOptions
		{
			SubjectNameStrategy = SubjectNameStrategy.TopicName,
			CustomSubjectNameStrategyType = null,
		};

		// Even though the method has [RequiresUnreferencedCode], the default path is AOT-safe
#pragma warning disable IL2026 // Suppress for test purposes
#pragma warning disable IL3050
		var strategy = options.CreateSubjectNameStrategy();
#pragma warning restore IL3050
#pragma warning restore IL2026
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void CreateSubjectNameStrategy_WithRecordNameStrategy_ReturnWithoutReflection()
	{
		var options = new ConfluentSchemaRegistryOptions
		{
			SubjectNameStrategy = SubjectNameStrategy.RecordName,
			CustomSubjectNameStrategyType = null,
		};

#pragma warning disable IL2026
#pragma warning disable IL3050
		var strategy = options.CreateSubjectNameStrategy();
#pragma warning restore IL3050
#pragma warning restore IL2026
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void DefaultTransportName_BeKafka()
	{
		KafkaTransportServiceCollectionExtensions.DefaultTransportName.ShouldBe("kafka");
	}

	[Fact]
	public void ConfluentSchemaRegistryOptions_DefaultValues_BeReasonable()
	{
		var options = new ConfluentSchemaRegistryOptions();

		options.Url.ShouldBe("http://localhost:8081");
		options.MaxCachedSchemas.ShouldBe(1000);
		options.AutoRegisterSchemas.ShouldBeTrue();
		options.CacheSchemas.ShouldBeTrue();
		options.ValidateBeforeRegister.ShouldBeTrue();
		options.CustomSubjectNameStrategyType.ShouldBeNull();
		options.SubjectNameStrategy.ShouldBe(SubjectNameStrategy.TopicName);
	}

	[Fact]
	public void BothAddKafkaTransportOverloads_HaveMatchingAnnotationMessages()
	{
		var extensionsType = typeof(KafkaTransportServiceCollectionExtensions);
		var methods = extensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "AddKafkaTransport")
			.ToList();

		methods.Count.ShouldBe(2, "Should have exactly 2 AddKafkaTransport overloads");

		var messages = methods
			.Select(m => m.GetCustomAttribute<RequiresUnreferencedCodeAttribute>()?.Message)
			.Where(m => m is not null)
			.Distinct()
			.ToList();

		// Both overloads should have the same annotation message for consistency
		messages.Count.ShouldBe(1, "Both overloads should have identical [RequiresUnreferencedCode] messages");
	}

	[Fact]
	public void RequiresUnreferencedCode_AndRequiresDynamicCode_AlwaysAppliedTogether()
	{
		// Per .NET guidelines, these two attributes should always be applied together
		var extensionsType = typeof(KafkaTransportServiceCollectionExtensions);
		var methods = extensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "AddKafkaTransport");

		foreach (var method in methods)
		{
			var hasUnreferenced = method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>() is not null;
			var hasDynamic = method.GetCustomAttribute<RequiresDynamicCodeAttribute>() is not null;

			// If one is present, both should be
			hasUnreferenced.ShouldBe(hasDynamic,
				$"AddKafkaTransport ({method.GetParameters().Length} params): [RequiresUnreferencedCode]={hasUnreferenced} but [RequiresDynamicCode]={hasDynamic}");
		}
	}

	[Fact]
	public void CreateSubjectNameStrategy_RequiresUnreferencedCode_AndRequiresDynamicCode_AppliedTogether()
	{
		var method = typeof(ConfluentSchemaRegistryOptions).GetMethod("CreateSubjectNameStrategy");
		method.ShouldNotBeNull();

		var hasUnreferenced = method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>() is not null;
		var hasDynamic = method.GetCustomAttribute<RequiresDynamicCodeAttribute>() is not null;

		hasUnreferenced.ShouldBeTrue();
		hasDynamic.ShouldBeTrue();
	}
}
