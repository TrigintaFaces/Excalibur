// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions;

namespace Excalibur.Cdc;

/// <summary>
/// The exception that is thrown when a CDC event mapping operation fails.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown by <see cref="CdcDataChangeExtensions"/> when a column
/// is not found in the change data, or by <see cref="ICdcEventMapper{TEvent}"/>
/// implementations when mapping fails due to type mismatches or missing data.
/// </para>
/// </remarks>
public sealed class CdcMappingException : ResourceException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CdcMappingException"/> class.
	/// </summary>
	public CdcMappingException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcMappingException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public CdcMappingException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcMappingException"/> class
	/// with a specified error message and inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public CdcMappingException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
