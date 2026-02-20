// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

/// <summary>
/// Collection definition for SQL Server provider integration tests.
/// Shares SqlServerContainerFixture across all tests in this collection.
/// </summary>
/// <remarks>
/// Sprint 175 - Provider Testing Epic Phase 1.
/// This collection definition must be in the same assembly as the test classes
/// that use [Collection(ContainerCollections.SqlServer)].
/// </remarks>
[CollectionDefinition(ContainerCollections.SqlServer)]
public class SqlServerProviderTestCollection : ICollectionFixture<SqlServerFixture>
{
}
