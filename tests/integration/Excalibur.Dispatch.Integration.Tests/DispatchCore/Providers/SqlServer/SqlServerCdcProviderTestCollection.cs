// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

/// <summary>
/// Binds the Agent-enabled SQL Server fixture to the CDC collection, in THIS assembly.
/// </summary>
/// <remarks>
/// <para>
/// xUnit resolves a collection definition only within the assembly that declares it. The identical
/// definition in <c>Tests.Shared</c> is therefore invisible here, and a test class carrying
/// <c>[Collection(ContainerCollections.SqlServerCdc)]</c> without this file gets NO fixture: the
/// analyzer reports xUnit1041 as a warning and the arm fails at construction rather than at its
/// assertion, which reads as a defect in the code under test. Every sibling collection in this
/// project is re-declared for the same reason.
/// </para>
/// </remarks>
[CollectionDefinition(ContainerCollections.SqlServerCdc)]
public class SqlServerCdcProviderTestCollection : ICollectionFixture<SqlServerCdcContainerFixture>
{
}
