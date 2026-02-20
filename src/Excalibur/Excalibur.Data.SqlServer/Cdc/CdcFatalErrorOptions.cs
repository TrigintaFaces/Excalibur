// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Options for configuring fatal error handling during CDC processing.
/// </summary>
public sealed class CdcFatalErrorOptions
{
	/// <summary>
	/// Gets or sets the delegate invoked when a fatal error occurs during CDC processing.
	/// When <see langword="null"/>, the processor rethrows the exception and stops processing.
	/// </summary>
	public CdcFatalErrorHandler? OnFatalError { get; set; }
}
