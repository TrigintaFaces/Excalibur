// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.DeadLetter;

/// <summary>
/// xUnit collection definition for SqlServer dead-letter-queue integration tests.
/// Collection definitions must be in the same assembly as the tests that reference them.
/// </summary>
[CollectionDefinition(CollectionName)]
public class SqlServerDeadLetterTestCollection : ICollectionFixture<SqlServerContainerFixture>
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = "SqlServer DeadLetter Integration Tests";
}
