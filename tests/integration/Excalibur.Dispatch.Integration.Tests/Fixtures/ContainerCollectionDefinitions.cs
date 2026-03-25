// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Fixtures;

/// <summary>
/// xUnit collection definitions for sharing TestContainer fixtures across integration test classes.
/// These must be in the same assembly as the test classes that use them.
/// The fixture implementations come from Tests.Shared.
/// </summary>
[CollectionDefinition(ContainerCollections.Kafka)]
public sealed class KafkaCollection : ICollectionFixture<KafkaContainerFixture>;

/// <summary>
/// Collection definition for Azure Service Bus integration tests.
/// Serializes execution across test classes that share the emulator container.
/// </summary>
[CollectionDefinition(ContainerCollections.AzureServiceBus)]
public sealed class AzureServiceBusCollection;

/// <summary>
/// Collection definition for AWS SQS integration tests.
/// Serializes execution across test classes that share the LocalStack container.
/// </summary>
[CollectionDefinition(ContainerCollections.AwsSqs)]
public sealed class AwsSqsCollection;
