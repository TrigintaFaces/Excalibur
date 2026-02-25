// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Azure;

/// <summary>
/// Event IDs for Azure Key Vault RSA key wrapping operations (92630-92649).
/// </summary>
public static class AzureRsaKeyWrappingEventId
{
	/// <summary>RSA key wrapper initialized.</summary>
	public const int RsaKeyWrapperInitialized = 92630;

	/// <summary>Data encryption key wrapped successfully.</summary>
	public const int KeyWrapped = 92631;

	/// <summary>Failed to wrap data encryption key.</summary>
	public const int KeyWrapFailed = 92632;

	/// <summary>Data encryption key unwrapped successfully.</summary>
	public const int KeyUnwrapped = 92633;

	/// <summary>Failed to unwrap data encryption key.</summary>
	public const int KeyUnwrapFailed = 92634;

	/// <summary>RSA key wrapper disposed.</summary>
	public const int RsaKeyWrapperDisposed = 92635;
}
