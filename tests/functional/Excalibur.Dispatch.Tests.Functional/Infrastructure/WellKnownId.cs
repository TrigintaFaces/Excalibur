// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.Infrastructure;

/// <summary>
///     Well-known identifiers used throughout the tests
/// </summary>
public static class WellKnownId
{
	/// <summary>
	///     Test tenant identifier
	/// </summary>
	public const string TestTenant = nameof(TestTenant);

	/// <summary>
	///     Local user identifier for testing
	/// </summary>
	public const string LocalUser = "local@dispatch.services";

	/// <summary>
	///     Test correlation ID
	/// </summary>
	public const string TestCorrelationId = "test-correlation-id";

	/// <summary>
	///     Test user ID
	/// </summary>
	public const string TestUserId = "test-user-id";
}
