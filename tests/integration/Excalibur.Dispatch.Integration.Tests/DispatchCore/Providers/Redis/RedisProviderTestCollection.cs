// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Redis;

/// <summary>
/// Collection definition for Redis provider integration tests.
/// Shares RedisContainerFixture across all tests in this collection.
/// </summary>
/// <remarks>
/// Sprint 177 - Provider Testing Epic Phase 3.
/// This collection definition must be in the same assembly as the test classes
/// that use [Collection(ContainerCollections.Redis)].
/// </remarks>
[CollectionDefinition(ContainerCollections.Redis)]
public class RedisProviderTestCollection : ICollectionFixture<RedisContainerFixture>
{
}
