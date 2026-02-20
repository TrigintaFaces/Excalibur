// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// Cross-transport verification that all 5 transports implement <see cref="ITransportSubscriber"/>
/// from Transport.Abstractions.
/// <para>
/// Sprint 529: ITransportSubscriber implemented for all 5 transports (Kafka, Azure SB, RabbitMQ,
/// AWS SQS, Google PubSub). This test ensures universality is maintained.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportSubscriberUniversalityShould
{
	private static readonly string[] TransportAssemblyNames =
	[
		"Excalibur.Dispatch.Transport.Kafka",
		"Excalibur.Dispatch.Transport.AzureServiceBus",
		"Excalibur.Dispatch.Transport.AwsSqs",
		"Excalibur.Dispatch.Transport.RabbitMQ",
		"Excalibur.Dispatch.Transport.GooglePubSub",
	];

	[Fact]
	public void FiveTransports_ImplementITransportSubscriber()
	{
		// Arrange -- ensure all transport assemblies are loaded
		foreach (var assemblyName in TransportAssemblyNames)
		{
			Assembly.Load(assemblyName);
		}

		// Act -- find all types implementing ITransportSubscriber
		var implementations = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => a.GetName().Name?.StartsWith("Excalibur.Dispatch.Transport.", StringComparison.Ordinal) == true)
			.SelectMany(a =>
			{
				try { return a.GetTypes(); }
				catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null).Cast<Type>(); }
			})
			.Where(t => t.IsClass && !t.IsAbstract && typeof(ITransportSubscriber).IsAssignableFrom(t))
			.Select(t => t.FullName)
			.OrderBy(n => n, StringComparer.Ordinal)
			.ToList();

		// Assert -- 5 implementations (one per transport)
		implementations.Count.ShouldBeGreaterThanOrEqualTo(5,
			$"Expected 5+ ITransportSubscriber implementations, found {implementations.Count}: " +
			string.Join(", ", implementations));

		// Verify each transport assembly contributes at least one implementation
		foreach (var assemblyName in TransportAssemblyNames)
		{
			var assembly = AppDomain.CurrentDomain.GetAssemblies()
				.First(a => a.GetName().Name == assemblyName);

			var transportImpls = assembly.GetTypes()
				.Where(t => t.IsClass && !t.IsAbstract && typeof(ITransportSubscriber).IsAssignableFrom(t))
				.ToList();

			transportImpls.Count.ShouldBeGreaterThan(0,
				$"Transport assembly '{assemblyName}' should have at least one ITransportSubscriber implementation");
		}
	}

	[Fact]
	public void AllTransportSubscribers_ImplementIAsyncDisposable()
	{
		// Arrange
		foreach (var assemblyName in TransportAssemblyNames)
		{
			Assembly.Load(assemblyName);
		}

		var implementations = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => TransportAssemblyNames.Contains(a.GetName().Name))
			.SelectMany(a =>
			{
				try { return a.GetTypes(); }
				catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null).Cast<Type>(); }
			})
			.Where(t => t.IsClass && !t.IsAbstract && typeof(ITransportSubscriber).IsAssignableFrom(t))
			.ToList();

		// Assert -- all ITransportSubscriber implementations should also implement IAsyncDisposable
		foreach (var impl in implementations)
		{
			typeof(IAsyncDisposable).IsAssignableFrom(impl).ShouldBeTrue(
				$"{impl.FullName} implements ITransportSubscriber but not IAsyncDisposable");
		}
	}

	[Fact]
	public void AllTransportSubscribers_HaveSourceProperty()
	{
		// Arrange
		foreach (var assemblyName in TransportAssemblyNames)
		{
			Assembly.Load(assemblyName);
		}

		var implementations = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => TransportAssemblyNames.Contains(a.GetName().Name))
			.SelectMany(a =>
			{
				try { return a.GetTypes(); }
				catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null).Cast<Type>(); }
			})
			.Where(t => t.IsClass && !t.IsAbstract && typeof(ITransportSubscriber).IsAssignableFrom(t))
			.ToList();

		// Assert -- all implementations should expose the Source property
		foreach (var impl in implementations)
		{
			var sourceProperty = impl.GetProperty("Source", BindingFlags.Public | BindingFlags.Instance);
			sourceProperty.ShouldNotBeNull(
				$"{impl.FullName} implements ITransportSubscriber but has no public Source property");
			sourceProperty.PropertyType.ShouldBe(typeof(string),
				$"{impl.FullName}.Source should be of type string");
		}
	}

	[Fact]
	public void AllTransportSubscribers_HaveGetServiceMethod()
	{
		// Arrange
		foreach (var assemblyName in TransportAssemblyNames)
		{
			Assembly.Load(assemblyName);
		}

		var implementations = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => TransportAssemblyNames.Contains(a.GetName().Name))
			.SelectMany(a =>
			{
				try { return a.GetTypes(); }
				catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null).Cast<Type>(); }
			})
			.Where(t => t.IsClass && !t.IsAbstract && typeof(ITransportSubscriber).IsAssignableFrom(t))
			.ToList();

		// Assert -- all implementations should have GetService (explicit or implicit)
		foreach (var impl in implementations)
		{
			var getServiceMethod = impl.GetMethod("GetService",
				BindingFlags.Public | BindingFlags.Instance,
				null,
				[typeof(Type)],
				null);

			getServiceMethod.ShouldNotBeNull(
				$"{impl.FullName} implements ITransportSubscriber but has no public GetService(Type) method");
		}
	}
}
