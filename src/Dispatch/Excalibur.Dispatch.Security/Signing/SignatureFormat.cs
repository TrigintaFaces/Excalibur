// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Defines signature encoding formats.
/// </summary>
public enum SignatureFormat
{
	/// <summary>
	/// Base64 encoded signature.
	/// </summary>
	Base64 = 0,

	/// <summary>
	/// Hexadecimal encoded signature.
	/// </summary>
	Hex = 1,

	/// <summary>
	/// Raw binary signature.
	/// </summary>
	Binary = 2,
}
