// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// Represents the result of a policy evaluation.
/// </summary>
public sealed record PolicyResult
{
	/// <summary>
	/// Gets a value indicating whether the activity is authorized.
	/// </summary>
	/// <value><see langword="true"/> if the activity is authorized; otherwise, <see langword="false"/>.</value>
	public bool IsAuthorized { get; init; }

	/// <summary>
	/// Gets a value indicating whether the activity grant exists.
	/// </summary>
	/// <value><see langword="true"/> if the activity grant exists; otherwise, <see langword="false"/>.</value>
	public bool HasActivityGrant { get; init; }

	/// <summary>
	/// Gets a value indicating whether the resource grant exists.
	/// </summary>
	/// <value><see langword="true"/> if the resource grant exists; otherwise, <see langword="false"/>.</value>
	public bool HasResourceGrant { get; init; }
}
