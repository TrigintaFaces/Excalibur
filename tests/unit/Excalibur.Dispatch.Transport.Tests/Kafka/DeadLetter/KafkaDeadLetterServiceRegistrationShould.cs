// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.DeadLetter;

/// <summary>
/// Verifies DI registration for Kafka DLQ services (S523.7).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KafkaDeadLetterServiceRegistrationShould
{
	[Fact]
	public void AddKafkaDeadLetterQueue_RegistersIDeadLetterQueueManager()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddKafkaDeadLetterQueue();

		// Assert
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IDeadLetterQueueManager));
		descriptor.ShouldNotBeNull("IDeadLetterQueueManager should be registered");
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddKafkaDeadLetterQueue_RegistersOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddKafkaDeadLetterQueue(opts =>
		{
			opts.TopicSuffix = ".dlq";
			opts.MaxDeliveryAttempts = 3;
		});

		// Assert — options configuration should be registered
		var optionsDescriptors = services
			.Where(d => d.ServiceType.IsGenericType &&
			            d.ServiceType.GetGenericTypeDefinition() == typeof(Microsoft.Extensions.Options.IConfigureOptions<>) &&
			            d.ServiceType.GetGenericArguments()[0] == typeof(KafkaDeadLetterOptions))
			.ToList();
		optionsDescriptors.ShouldNotBeEmpty("KafkaDeadLetterOptions configuration should be registered");
	}

	[Fact]
	public void AddKafkaDeadLetterQueue_WithNullConfigure_RegistersDefaultOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddKafkaDeadLetterQueue();

		// Assert - should not throw
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IDeadLetterQueueManager));
		descriptor.ShouldNotBeNull();
	}

	[Fact]
	public void AddKafkaDeadLetterQueue_ThrowsOnNullServices()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			KafkaDeadLetterServiceCollectionExtensions.AddKafkaDeadLetterQueue(null!));
	}

	[Fact]
	public void AddKafkaDeadLetterQueue_UsesTryAdd_NoDuplicateRegistration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - register twice
		services.AddKafkaDeadLetterQueue();
		services.AddKafkaDeadLetterQueue();

		// Assert - only one IDeadLetterQueueManager registration (TryAddSingleton)
		var managerDescriptors = services
			.Where(d => d.ServiceType == typeof(IDeadLetterQueueManager))
			.ToList();
		managerDescriptors.Count.ShouldBe(1, "TryAddSingleton should prevent duplicate registration");
	}
}
