// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// Cross-transport verification that transports implement <see cref="IDeadLetterQueueManager"/>
/// from Transport.Abstractions.
/// </summary>
/// <remarks>
/// All 5 transports implement the Transport.Abstractions <see cref="IDeadLetterQueueManager"/>:
/// Kafka, Azure SB, AWS SQS, RabbitMQ, and Google PubSub.
/// Google PubSub was aligned in Sprint 526 (S526.7).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DlqUniversalityShould
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
	public void FiveTransports_ImplementIDeadLetterQueueManager()
	{
		// Arrange — ensure all transport assemblies are loaded
		foreach (var assemblyName in TransportAssemblyNames)
		{
			Assembly.Load(assemblyName);
		}

		// Act — find all types implementing the Transport.Abstractions IDeadLetterQueueManager
		var implementations = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => a.GetName().Name?.StartsWith("Excalibur.Dispatch.Transport.", StringComparison.Ordinal) == true)
			.SelectMany(a =>
			{
				try { return a.GetTypes(); }
				catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null).Cast<Type>(); }
			})
			.Where(t => t.IsClass && !t.IsAbstract && typeof(IDeadLetterQueueManager).IsAssignableFrom(t))
			.Select(t => t.FullName)
			.OrderBy(n => n, StringComparer.Ordinal)
			.ToList();

		// Assert — 5 implementations (Kafka, Azure SB, AWS, RabbitMQ, Google PubSub)
		implementations.Count.ShouldBeGreaterThanOrEqualTo(5,
			$"Expected 5+ IDeadLetterQueueManager implementations, found {implementations.Count}: " +
			string.Join(", ", implementations));

		// Verify each transport assembly contributes at least one implementation
		foreach (var assemblyName in TransportAssemblyNames)
		{
			var assembly = AppDomain.CurrentDomain.GetAssemblies()
				.First(a => a.GetName().Name == assemblyName);

			var transportImpls = assembly.GetTypes()
				.Where(t => t.IsClass && !t.IsAbstract && typeof(IDeadLetterQueueManager).IsAssignableFrom(t))
				.ToList();

			transportImpls.Count.ShouldBeGreaterThan(0,
				$"Transport assembly '{assemblyName}' should have at least one IDeadLetterQueueManager implementation");
		}
	}

	[Fact]
	public void AllDlqManagers_ImplementIDisposable()
	{
		// Arrange — all 4 transports implementing the shared interface should also be IDisposable
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
			.Where(t => t.IsClass && !t.IsAbstract && typeof(IDeadLetterQueueManager).IsAssignableFrom(t))
			.ToList();

		// Assert — all DLQ managers should implement IDisposable
		foreach (var impl in implementations)
		{
			typeof(IDisposable).IsAssignableFrom(impl).ShouldBeTrue(
				$"{impl.FullName} implements IDeadLetterQueueManager but not IDisposable");
		}
	}
}
