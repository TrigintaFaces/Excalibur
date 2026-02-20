// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Persistence;

/// <summary>
/// Specifies the connection pool blocking period.
/// </summary>
public enum PoolBlockingPeriod
{
	/// <summary>
	/// Automatically determine the blocking period.
	/// </summary>
	Auto = 0,

	/// <summary>
	/// Always block when the pool is full.
	/// </summary>
	AlwaysBlock = 1,

	/// <summary>
	/// Never block when the pool is full.
	/// </summary>
	NeverBlock = 2,
}
