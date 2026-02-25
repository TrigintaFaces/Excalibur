// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Event IDs for <see cref="AspNetCoreAuthorizationMiddleware"/> log messages.
/// </summary>
/// <remarks>
/// Event IDs are allocated within the <c>Excalibur.Dispatch.Hosting.*</c> range (2600–2699).
/// </remarks>
internal static class AspNetCoreAuthorizationEventId
{
	/// <summary>
	/// Authorization evaluation is starting for a message.
	/// </summary>
	internal const int AuthorizationExecuting = 2600;

	/// <summary>
	/// Authorization succeeded for a message.
	/// </summary>
	internal const int AuthorizationGranted = 2601;

	/// <summary>
	/// Authorization was denied for a message.
	/// </summary>
	internal const int AuthorizationDenied = 2602;

	/// <summary>
	/// Authorization was skipped (middleware disabled or no attributes).
	/// </summary>
	internal const int AuthorizationSkipped = 2603;

	/// <summary>
	/// <c>[AllowAnonymous]</c> attribute was found; authorization bypassed.
	/// </summary>
	internal const int AllowAnonymousApplied = 2604;

	/// <summary>
	/// Attribute cache hit for a type lookup.
	/// </summary>
	internal const int AttributeCacheHit = 2605;

	/// <summary>
	/// An error occurred during authorization evaluation.
	/// </summary>
	internal const int AuthorizationError = 2606;
}
