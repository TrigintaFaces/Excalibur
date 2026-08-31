// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Benchmarks;

/// <summary>
/// The framework's single-tenant identity, supplied so a benchmark measures the same partitioning work a
/// single-tenant host actually performs rather than an unreachable unscoped path.
/// </summary>
internal sealed class BenchmarkTenantContext : ITenantContext
{
	public static readonly BenchmarkTenantContext SingleTenant = new();

	public string? TenantId => TenantDefaults.DefaultTenantId;

	public bool HasTenant => true;
}
