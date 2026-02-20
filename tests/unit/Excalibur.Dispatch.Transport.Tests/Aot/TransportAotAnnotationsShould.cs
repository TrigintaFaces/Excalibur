// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Aot;

/// <summary>
/// Verifies that Sprint 522 AOT annotations are correctly applied to transport DI extension methods.
/// Per AD-522.1: Only Kafka SchemaRegistry uses Activator.CreateInstance, so only Kafka transport
/// DI methods need [RequiresUnreferencedCode] + [RequiresDynamicCode].
/// RabbitMQ, Azure, AWS, Google are verified AOT-safe (no reflection risk on DI entry points).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.AOT")]
public sealed class TransportAotAnnotationsShould
{
	// Assembly name constants matching csproj references
	private const string KafkaAssembly = "Excalibur.Dispatch.Transport.Kafka";
	private const string RabbitMQAssembly = "Excalibur.Dispatch.Transport.RabbitMQ";
	private const string AzureAssembly = "Excalibur.Dispatch.Transport.AzureServiceBus";
	private const string AwsAssembly = "Excalibur.Dispatch.Transport.AwsSqs";
	private const string GoogleAssembly = "Excalibur.Dispatch.Transport.GooglePubSub";

	#region Kafka Transport — Must Have Annotations (S522.6)

	[Fact]
	public void KafkaTransport_AddKafkaTransportNamed_HaveRequiresUnreferencedCode()
	{
		var method = GetKafkaTransportMethod("AddKafkaTransport", 3);

		var attr = method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>();
		attr.ShouldNotBeNull("AddKafkaTransport(services, name, configure) should have [RequiresUnreferencedCode]");
		attr.Message.ShouldContain("Activator.CreateInstance", Case.Insensitive);
	}

	[Fact]
	public void KafkaTransport_AddKafkaTransportNamed_HaveRequiresDynamicCode()
	{
		var method = GetKafkaTransportMethod("AddKafkaTransport", 3);

		var attr = method.GetCustomAttribute<RequiresDynamicCodeAttribute>();
		attr.ShouldNotBeNull("AddKafkaTransport(services, name, configure) should have [RequiresDynamicCode]");
		attr.Message.ShouldContain("Activator.CreateInstance", Case.Insensitive);
	}

	[Fact]
	public void KafkaTransport_AddKafkaTransportDefault_HaveRequiresUnreferencedCode()
	{
		var method = GetKafkaTransportMethod("AddKafkaTransport", 2);

		var attr = method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>();
		attr.ShouldNotBeNull("AddKafkaTransport(services, configure) should have [RequiresUnreferencedCode]");
	}

	[Fact]
	public void KafkaTransport_AddKafkaTransportDefault_HaveRequiresDynamicCode()
	{
		var method = GetKafkaTransportMethod("AddKafkaTransport", 2);

		var attr = method.GetCustomAttribute<RequiresDynamicCodeAttribute>();
		attr.ShouldNotBeNull("AddKafkaTransport(services, configure) should have [RequiresDynamicCode]");
	}

	#endregion

	#region ConfluentSchemaRegistryOptions — CreateSubjectNameStrategy (S522.6)

	[Fact]
	public void SchemaRegistryOptions_CreateSubjectNameStrategy_HaveRequiresUnreferencedCode()
	{
		var method = typeof(ConfluentSchemaRegistryOptions).GetMethod("CreateSubjectNameStrategy");
		method.ShouldNotBeNull();

		var attr = method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>();
		attr.ShouldNotBeNull("CreateSubjectNameStrategy() should have [RequiresUnreferencedCode]");
		attr.Message.ShouldContain("Activator.CreateInstance", Case.Insensitive);
	}

	[Fact]
	public void SchemaRegistryOptions_CreateSubjectNameStrategy_HaveRequiresDynamicCode()
	{
		var method = typeof(ConfluentSchemaRegistryOptions).GetMethod("CreateSubjectNameStrategy");
		method.ShouldNotBeNull();

		var attr = method.GetCustomAttribute<RequiresDynamicCodeAttribute>();
		attr.ShouldNotBeNull("CreateSubjectNameStrategy() should have [RequiresDynamicCode]");
	}

	[Fact]
	public void SchemaRegistryOptions_CustomSubjectNameStrategyType_HaveDynamicallyAccessedMembers()
	{
		var prop = typeof(ConfluentSchemaRegistryOptions).GetProperty("CustomSubjectNameStrategyType");
		prop.ShouldNotBeNull();

		var attr = prop.GetCustomAttribute<DynamicallyAccessedMembersAttribute>();
		attr.ShouldNotBeNull("CustomSubjectNameStrategyType should have [DynamicallyAccessedMembers]");
		attr.MemberTypes.ShouldBe(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor);
	}

	#endregion

	#region RabbitMQ Transport — Should NOT Have Annotations (AD-522.1: AOT-safe)

	[Fact]
	public void RabbitMQTransport_AddRabbitMQTransport_NotHaveRequiresUnreferencedCode()
	{
		var extensionsType = LoadExtensionsType(RabbitMQAssembly, "RabbitMQTransportServiceCollectionExtensions");
		extensionsType.ShouldNotBeNull();

		AssertNoAotAnnotation(extensionsType, "AddRabbitMQTransport", "RabbitMQ");
	}

	#endregion

	#region Azure Service Bus Transport — Should NOT Have Annotations (AD-522.1: AOT-safe)

	[Fact]
	public void AzureTransport_AddAzureServiceBusTransport_NotHaveRequiresUnreferencedCode()
	{
		var extensionsType = LoadExtensionsType(AzureAssembly, "AzureServiceBusTransportServiceCollectionExtensions");
		extensionsType.ShouldNotBeNull();

		AssertNoAotAnnotation(extensionsType, "AddAzureServiceBusTransport", "Azure");
	}

	#endregion

	#region AWS SQS Transport — Should NOT Have Annotations (AD-522.1: AOT-safe)

	[Fact]
	public void AwsTransport_AddAwsSqsTransport_NotHaveRequiresUnreferencedCode()
	{
		var extensionsType = LoadExtensionsType(AwsAssembly, "AwsSqsTransportServiceCollectionExtensions");
		extensionsType.ShouldNotBeNull();

		AssertNoAotAnnotation(extensionsType, "AddAwsSqsTransport", "AWS");
	}

	#endregion

	#region Google Pub/Sub Transport — Should NOT Have Annotations (AD-522.1: AOT-safe)

	[Fact]
	public void GoogleTransport_AddGooglePubSubTransport_NotHaveRequiresUnreferencedCode()
	{
		var extensionsType = LoadExtensionsType(GoogleAssembly, "GooglePubSubTransportServiceCollectionExtensions");
		extensionsType.ShouldNotBeNull();

		AssertNoAotAnnotation(extensionsType, "AddGooglePubSubTransport", "Google");
	}

	#endregion

	#region Cross-Transport Consistency

	[Fact]
	public void AllTransports_KafkaIsOnlyTransportWithAotAnnotations()
	{
		var transportAnnotationStatus = new Dictionary<string, bool>
		{
			["Kafka"] = HasAotAnnotation(KafkaAssembly, "KafkaTransportServiceCollectionExtensions", "AddKafkaTransport"),
			["RabbitMQ"] = HasAotAnnotation(RabbitMQAssembly, "RabbitMQTransportServiceCollectionExtensions", "AddRabbitMQTransport"),
			["AzureServiceBus"] = HasAotAnnotation(AzureAssembly, "AzureServiceBusTransportServiceCollectionExtensions", "AddAzureServiceBusTransport"),
			["AwsSqs"] = HasAotAnnotation(AwsAssembly, "AwsSqsTransportServiceCollectionExtensions", "AddAwsSqsTransport"),
			["GooglePubSub"] = HasAotAnnotation(GoogleAssembly, "GooglePubSubTransportServiceCollectionExtensions", "AddGooglePubSubTransport"),
		};

		transportAnnotationStatus["Kafka"].ShouldBeTrue("Kafka should have [RequiresUnreferencedCode]");
		transportAnnotationStatus["RabbitMQ"].ShouldBeFalse("RabbitMQ should NOT have [RequiresUnreferencedCode]");
		transportAnnotationStatus["AzureServiceBus"].ShouldBeFalse("AzureServiceBus should NOT have [RequiresUnreferencedCode]");
		transportAnnotationStatus["AwsSqs"].ShouldBeFalse("AwsSqs should NOT have [RequiresUnreferencedCode]");
		transportAnnotationStatus["GooglePubSub"].ShouldBeFalse("GooglePubSub should NOT have [RequiresUnreferencedCode]");
	}

	#endregion

	#region Helpers

	private static MethodInfo GetKafkaTransportMethod(string methodName, int paramCount)
	{
		var extensionsType = typeof(KafkaTransportServiceCollectionExtensions);
		var method = extensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == paramCount);
		method.ShouldNotBeNull($"Could not find {methodName} with {paramCount} parameters");
		return method;
	}

	/// <summary>
	/// Loads a DI extensions type by assembly name and simple class name.
	/// Uses Assembly.Load to ensure the assembly is loaded even if no types have been referenced yet.
	/// </summary>
	private static Type? LoadExtensionsType(string assemblyName, string simpleClassName)
	{
		var assembly = Assembly.Load(assemblyName);
		return assembly.GetType($"Microsoft.Extensions.DependencyInjection.{simpleClassName}");
	}

	private static void AssertNoAotAnnotation(Type extensionsType, string methodName, string transportName)
	{
		var methods = extensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == methodName)
			.ToList();
		methods.ShouldNotBeEmpty($"{transportName} should have {methodName} methods");

		foreach (var method in methods)
		{
			var attr = method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>();
			attr.ShouldBeNull($"{methodName} ({method.GetParameters().Length} params) should NOT have [RequiresUnreferencedCode] — {transportName} is AOT-safe");
		}
	}

	private static bool HasAotAnnotation(string assemblyName, string simpleClassName, string methodName)
	{
		var type = LoadExtensionsType(assemblyName, simpleClassName);
		if (type is null) return false;

		return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == methodName)
			.Any(m => m.GetCustomAttribute<RequiresUnreferencedCodeAttribute>() is not null);
	}

	#endregion
}
