// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// xUnit collection for the Oracle inbox suites, so they share one emulator container.
/// </summary>
/// <remarks>
/// A class fixture gives each suite its own container, and an Oracle image is among the heaviest and
/// slowest to start that this repository uses. Three of them coming up at once is enough contention to
/// make these suites fail under a full run while every one of them passes in isolation, which reads as
/// a store defect rather than as resource pressure. The suites address distinct tables, so one shared
/// container serves all of them without interference.
/// </remarks>
[CollectionDefinition(CollectionName)]
public class OracleInboxTestCollection : ICollectionFixture<OracleInboxStoreContainerFixture>
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = "Oracle Inbox Integration Tests";
}
