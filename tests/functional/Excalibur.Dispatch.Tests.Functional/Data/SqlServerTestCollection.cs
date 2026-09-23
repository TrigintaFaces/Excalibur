// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Tests.Functional.Data;

/// <summary>
/// xUnit test collection for SQL Server functional tests.
/// All tests in this collection share a single SQL Server container instance.
/// </summary>
[CollectionDefinition(CollectionName)]
public sealed class SqlServerTestCollection : ICollectionFixture<SqlServerContainerFixture>
{
	public const string CollectionName = "SqlServer Functional Tests";
}
