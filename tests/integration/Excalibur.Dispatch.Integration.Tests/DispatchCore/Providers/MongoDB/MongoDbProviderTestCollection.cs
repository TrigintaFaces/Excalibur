// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.MongoDB;

/// <summary>
/// Collection definition for MongoDB provider integration tests.
/// Shares MongoDbContainerFixture across all tests in this collection.
/// </summary>
/// <remarks>
/// Sprint 177 - Provider Testing Epic Phase 3.
/// This collection definition must be in the same assembly as the test classes
/// that use [Collection(ContainerCollections.MongoDB)].
/// </remarks>
[CollectionDefinition(ContainerCollections.MongoDB)]
public class MongoDbProviderTestCollection : ICollectionFixture<MongoDbContainerFixture>
{
}
