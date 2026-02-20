// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Exception thrown when pipeline synthesis fails.
/// </summary>
/// <remarks>Creates a new pipeline synthesis exception.</remarks>
/// <param name="message">The failure message.</param>
/// <param name="validationIssues">The validation issues that caused the failure.</param>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors",
	Justification = "This exception requires specific ValidationIssue[] data; standard constructors would allow invalid state.")]
public sealed class PipelineSynthesisException(string message, ValidationIssue[] validationIssues) : Exception(message)
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PipelineSynthesisException"/> class.
	/// </summary>
	public PipelineSynthesisException() : this(string.Empty, [])
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PipelineSynthesisException"/> class with a message.
	/// </summary>
	/// <param name="message">The failure message.</param>
	public PipelineSynthesisException(string? message) : this(message ?? string.Empty, [])
	{
	}

	// R0.8: Remove unused parameter - Required to maintain standard exception constructor pattern
#pragma warning disable IDE0060
	/// <summary>
	/// Initializes a new instance of the <see cref="PipelineSynthesisException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The failure message.</param>
	/// <param name="innerException">The inner exception.</param>
	public PipelineSynthesisException(string? message, Exception? innerException) : this(message ?? string.Empty, [])
	{
	}
#pragma warning restore IDE0060

	/// <summary>
	/// Gets the validation issues that caused the synthesis failure.
	/// </summary>
	/// <value>
	/// The validation issues that caused the synthesis failure.
	/// </value>
	public ValidationIssue[] ValidationIssues { get; } = validationIssues ?? throw new ArgumentNullException(nameof(validationIssues));
}
