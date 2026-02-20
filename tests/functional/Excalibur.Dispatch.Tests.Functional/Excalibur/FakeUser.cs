// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.Excalibur;

/// <summary>
///     Fake user for testing.
/// </summary>
public class FakeUser
{
	/// <summary>
	///     Gets or sets the user MessageId.
	/// </summary>
	public required string Id { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the user name.
	/// </summary>
	public required string Name { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the user email.
	/// </summary>
	public required string Email { get; set; } = string.Empty;
}
