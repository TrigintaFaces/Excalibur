// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.Excalibur;

/// <summary>
///     Fake access Token for testing purposes
/// </summary>
public class FakeAccessToken
{
	/// <summary>
	///     Gets or sets the Token value
	/// </summary>
	public required string Token { get; set; } = "fake-this.Token";

	/// <summary>
	///     Gets or sets the expiry time
	/// </summary>
	public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(1);

	/// <summary>
	///     Gets a value indicating whether the Token is expired
	/// </summary>
	public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}
