// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows.SqlServer.Tests;

/// <summary>
/// xUnit collection definition for the SQL Server workflow signal-inbox integration tests. Collection
/// definitions must live in the same assembly as the tests that use them.
/// </summary>
[CollectionDefinition(CollectionName)]
public class SqlServerWorkflowSignalInboxTestCollection : ICollectionFixture<SqlServerWorkflowSignalInboxContainerFixture>
{
    /// <summary>
    /// The collection name used by test classes.
    /// </summary>
    public const string CollectionName = "SqlServer WorkflowSignalInbox Integration Tests";
}
