// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Testing.Containers;

/// <summary>
/// Identifies the database engine provided by an <see cref="IDatabaseContainerFixture"/>.
/// </summary>
public enum DatabaseEngine
{
	/// <summary>Microsoft SQL Server database engine.</summary>
	SqlServer,

	/// <summary>PostgreSQL database engine.</summary>
	Postgres,
}
