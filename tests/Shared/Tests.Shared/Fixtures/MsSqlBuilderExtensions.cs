// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Testcontainers.MsSql;

namespace Tests.Shared.Fixtures;

/// <summary>
/// Shared configuration for SQL Server test containers.
/// </summary>
public static class MsSqlBuilderExtensions
{
	/// <summary>
	/// The per-instance memory ceiling, in megabytes, applied to every SQL Server test container.
	/// </summary>
	/// <remarks>
	/// 2 GB is the minimum SQL Server supports; the conformance suites store a handful of rows and
	/// never approach it.
	/// </remarks>
	public const int MemoryLimitMb = 2048;

	/// <summary>
	/// Bounds the container's SQL Server instance to <see cref="MemoryLimitMb"/> so many instances can
	/// run at once.
	/// </summary>
	/// <remarks>
	/// <para>
	/// SQL Server on Linux sizes its buffer pool from TOTAL HOST memory, not from the container's share.
	/// Unbounded, every instance provisions as though it owned the machine, so instances beyond the point
	/// where those reservations exceed physical memory abort during startup — SIGABRT (exit 134), with a
	/// core dump written inside the container, or exit 1.
	/// </para>
	/// <para>
	/// The failure is a property of how many instances run at once, not of how fast they are started, and
	/// it surfaces far from its cause: the container dies during init, so every test in the collection
	/// fails on <c>InitializeAsync</c> with a generic startup-budget message that says nothing about
	/// memory.
	/// </para>
	/// <para>
	/// Measured on a 64 GB Docker allocation, 20 containers started simultaneously:
	/// </para>
	/// <code>
	/// unbounded                    12 of 20 up, 8 exited (5x SIGABRT, 3x exit 1)
	/// MSSQL_MEMORY_LIMIT_MB=2048   20 of 20 up, 0 exited
	/// </code>
	/// <para>
	/// Rate-limiting startup does NOT fix it — gating 20 starts to 8 at a time still yielded exactly 12
	/// survivors, because the constraint is on instances running concurrently. Note also that a test below
	/// the ceiling proves nothing: at 6 concurrent, bounded and unbounded both start cleanly.
	/// </para>
	/// </remarks>
	/// <param name="builder">The builder to configure.</param>
	/// <returns>The configured builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
	public static MsSqlBuilder WithBoundedMemory(this MsSqlBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithEnvironment(
			"MSSQL_MEMORY_LIMIT_MB",
			MemoryLimitMb.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}
}
