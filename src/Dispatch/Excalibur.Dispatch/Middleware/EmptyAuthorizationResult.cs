// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Empty implementation of IAuthorizationResult for contexts with no primary context.
/// </summary>
internal sealed class EmptyAuthorizationResult : IAuthorizationResult
{
	/// <inheritdoc/>
	public string? FailureMessage { get; }

	/// <inheritdoc/>
	public bool IsAuthorized { get; } = true;
}
