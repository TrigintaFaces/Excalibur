// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.Kafka;
using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Tests.Conformance.Providers.CrossTransport;

/// <summary>
/// Verifies all 5 transports have CloudEvents support:
/// - At least one <c>ICloudEventMapper&lt;T&gt;</c> implementation
/// - A CloudEvents adapter class
/// - A CloudEvents DI extension method
/// </summary>
public class CloudEventsConformanceShould
{
	private static readonly Type s_cloudEventMapperOpenGeneric = typeof(ICloudEventMapper<>);

	private static readonly (string Name, Assembly Assembly)[] s_transports =
	[
		("AzureServiceBus", typeof(AzureServiceBusOptions).Assembly),
		("AwsSqs", typeof(AwsSqsOptions).Assembly),
		("GooglePubSub", typeof(GooglePubSubOptions).Assembly),
		("Kafka", typeof(KafkaOptions).Assembly),
		("RabbitMQ", typeof(RabbitMqOptions).Assembly),
	];

	[Theory]
	[MemberData(nameof(TransportNames))]
	public void Have_CloudEventMapper_Implementation(string transportName)
	{
		var assembly = GetAssembly(transportName);
		var mapperTypes = FindCloudEventMapperImplementations(assembly);
		mapperTypes.Length.ShouldBeGreaterThan(
			0, $"{transportName} transport MUST have at least one ICloudEventMapper<T> implementation");
	}

	[Theory]
	[MemberData(nameof(TransportNames))]
	public void Have_CloudEventAdapter_Class(string transportName)
	{
		var assembly = GetAssembly(transportName);
		var adapterTypes = assembly.GetTypes()
			.Where(t => t is { IsAbstract: false, IsInterface: false }
				&& t.Name.Contains("CloudEventAdapter", StringComparison.OrdinalIgnoreCase))
			.ToArray();

		adapterTypes.Length.ShouldBeGreaterThan(
			0, $"{transportName} transport MUST have at least one CloudEventAdapter class");
	}

	[Theory]
	[MemberData(nameof(TransportNames))]
	public void Have_CloudEvents_DI_Extension(string transportName)
	{
		var assembly = GetAssembly(transportName);
		var extensionTypes = assembly.GetTypes()
			.Where(t => t.IsAbstract && t.IsSealed && t.Name.Contains("CloudEvents", StringComparison.OrdinalIgnoreCase))
			.ToArray();

		extensionTypes.Length.ShouldBeGreaterThan(
			0, $"{transportName} transport MUST have a CloudEvents DI extension class (static class with 'CloudEvents' in name)");
	}

	[Theory]
	[MemberData(nameof(TransportNames))]
	public void Have_CloudEventOptions_Class(string transportName)
	{
		var assembly = GetAssembly(transportName);
		var optionsTypes = assembly.GetTypes()
			.Where(t => t is { IsAbstract: false, IsInterface: false }
				&& t.Name.Contains("CloudEventOptions", StringComparison.OrdinalIgnoreCase))
			.ToArray();

		optionsTypes.Length.ShouldBeGreaterThan(
			0, $"{transportName} transport MUST have a CloudEventOptions configuration class");
	}

	public static TheoryData<string> TransportNames()
	{
		var data = new TheoryData<string>();
		foreach (var (name, _) in s_transports)
		{
			data.Add(name);
		}

		return data;
	}

	private static Assembly GetAssembly(string transportName) =>
		s_transports.First(t => t.Name == transportName).Assembly;

	private static Type[] FindCloudEventMapperImplementations(Assembly assembly) =>
		assembly.GetTypes()
			.Where(t => t is { IsAbstract: false, IsInterface: false }
				&& t.GetInterfaces().Any(i =>
					i.IsGenericType && i.GetGenericTypeDefinition() == s_cloudEventMapperOpenGeneric))
			.ToArray();
}
