// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Data.Tests.SqlServer;

/// <summary>
/// The framework single-tenant identity, supplied where a test is not exercising tenancy so the parameter
/// under test is the one that fails.
/// </summary>
internal sealed class TestTenantContext : ITenantContext
{
	public static readonly TestTenantContext SingleTenant = new();

	public string? TenantId => TenantDefaults.DefaultTenantId;

	public bool HasTenant => true;
}
