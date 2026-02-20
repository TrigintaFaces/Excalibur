// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Copyright (c) TrigintaFaces. All rights reserved.

namespace Excalibur.Security.Abstractions;

/// <summary>
/// Result of password verification.
/// </summary>
public enum PasswordVerificationResult
{
	/// <summary>
	/// Password is incorrect.
	/// </summary>
	Failed = 0,

	/// <summary>
	/// Password is correct.
	/// </summary>
	Success = 1,

	/// <summary>
	/// Password is correct but should be rehashed because the algorithm
	/// parameters have changed since the hash was created.
	/// </summary>
	/// <remarks>
	/// When this result is returned, the application should update the stored
	/// hash with a new one created using the current parameters. This enables
	/// gradual migration to stronger hashing parameters without requiring
	/// all users to reset their passwords.
	/// </remarks>
	SuccessRehashNeeded = 2,
}
