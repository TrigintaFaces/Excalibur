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
