// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Abstractions;

/// <summary>
/// Represents an exception that is thrown when an operation fails to execute successfully on a specific resource.
/// </summary>
/// <remarks>
/// This exception can be used to encapsulate information about failed operations, including the name of the operation and the resource involved.
/// </remarks>
[Serializable]
public sealed class OperationFailedException : ResourceException
{
	/// <summary>
	/// The default HTTP status code for operation failures.
	/// </summary>
	[NonSerialized]
	public const int DefaultStatusCode = 500;

	/// <summary>
	/// The default error message for operation failures.
	/// </summary>
	[NonSerialized]
	public const string DefaultMessage = "The operation failed.";

	/// <summary>
	/// Initializes a new instance of the <see cref="OperationFailedException" /> class.
	/// </summary>
	/// <param name="operation"> The name of the operation that failed. </param>
	/// <param name="resource"> The name or type of the resource associated with the operation. </param>
	/// <param name="statusCode"> The HTTP status code associated with the failure. Defaults to <see cref="DefaultStatusCode" />. </param>
	/// <param name="message"> A custom error message describing the failure. Defaults to <see cref="DefaultMessage" />. </param>
	/// <param name="innerException"> The inner exception that caused this exception, if applicable. </param>
	public OperationFailedException(string operation, string resource, int? statusCode = null, string? message = null,
		Exception? innerException = null)
		: base(resource, statusCode ?? DefaultStatusCode, message ?? DefaultMessage, innerException)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(resource);

		Operation = operation;
	}

	/// <inheritdoc/>
	public OperationFailedException(string resource, int? statusCode, string? message = null, Exception? innerException = null) : base(resource, statusCode, message, innerException)
	{
	}

	/// <summary>
	/// Gets or sets the name of the operation that failed.
	/// </summary>
	/// <value> A string representing the operation name. </value>
	public string Operation { get; set; }
}
