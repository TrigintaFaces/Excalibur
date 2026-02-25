// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Data.Firestore.Cdc;

/// <summary>
/// Exception thrown when the Firestore CDC processor detects a stale position
/// that cannot be recovered automatically.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown in the following scenarios:
/// <list type="bullet">
/// <item><description>The recovery strategy is <see cref="StalePositionRecoveryStrategy.Throw"/></description></item>
/// <item><description>The maximum recovery attempts have been exhausted</description></item>
/// <item><description>The callback strategy throws or fails to provide a valid new position</description></item>
/// </list>
/// </para>
/// <para>
/// The <see cref="EventArgs"/> property contains detailed information about the stale position,
/// including the reason code, affected positions, and original exception.
/// </para>
/// </remarks>
public sealed class FirestoreStalePositionException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreStalePositionException"/> class.
	/// </summary>
	public FirestoreStalePositionException()
		: base("The Firestore CDC processor detected a stale position that cannot be recovered.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreStalePositionException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public FirestoreStalePositionException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreStalePositionException"/> class
	/// with a specified error message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public FirestoreStalePositionException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreStalePositionException"/> class
	/// with the specified event arguments.
	/// </summary>
	/// <param name="eventArgs">The stale position event arguments.</param>
	public FirestoreStalePositionException(CdcPositionResetEventArgs eventArgs)
		: base(FormatMessage(eventArgs), eventArgs?.OriginalException)
	{
		EventArgs = eventArgs;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreStalePositionException"/> class
	/// with a custom message and event arguments.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="eventArgs">The stale position event arguments.</param>
	public FirestoreStalePositionException(string message, CdcPositionResetEventArgs eventArgs)
		: base(message, eventArgs?.OriginalException)
	{
		EventArgs = eventArgs;
	}

	/// <summary>
	/// Gets the event arguments containing details about the stale position scenario.
	/// </summary>
	/// <value>
	/// The <see cref="CdcPositionResetEventArgs"/> with detailed information,
	/// or <see langword="null"/> if not provided.
	/// </value>
	public CdcPositionResetEventArgs? EventArgs { get; }

	/// <summary>
	/// Gets the processor ID that detected the stale position.
	/// </summary>
	/// <value>The processor ID, or <see langword="null"/> if not available.</value>
	public string? ProcessorId => EventArgs?.ProcessorId;

	/// <summary>
	/// Gets the reason code for the stale position.
	/// </summary>
	/// <value>The reason code, or <see langword="null"/> if not available.</value>
	public string? ReasonCode => EventArgs?.ReasonCode;

	/// <summary>
	/// Gets the stale position bytes that were detected.
	/// </summary>
	/// <value>The stale position as bytes, or <see langword="null"/> if not available.</value>
	public byte[]? StalePositionBytes => EventArgs?.StalePosition;

	/// <summary>
	/// Gets the gRPC status code that indicated the stale position.
	/// </summary>
	/// <value>The gRPC status code, or <see langword="null"/> if not available.</value>
	public int? GrpcStatusCode => GetAdditionalContextValue<int>("GrpcStatusCode");

	/// <summary>
	/// Gets the Firestore project ID that was affected.
	/// </summary>
	/// <value>The project ID, or <see langword="null"/> if not available.</value>
	public string? ProjectId => EventArgs?.DatabaseName;

	/// <summary>
	/// Gets the collection path that was affected.
	/// </summary>
	/// <value>The collection path, or <see langword="null"/> if not available.</value>
	public string? CollectionPath => EventArgs?.CaptureInstance;

	/// <summary>
	/// Gets the document ID that was affected.
	/// </summary>
	/// <value>The document ID, or <see langword="null"/> if not available.</value>
	public string? DocumentId => GetAdditionalContextValue<string>("DocumentId");

	private T? GetAdditionalContextValue<T>(string key)
	{
		if (EventArgs?.AdditionalContext is null)
		{
			return default;
		}

		if (EventArgs.AdditionalContext.TryGetValue(key, out var value) && value is T typedValue)
		{
			return typedValue;
		}

		return default;
	}

	private static string FormatMessage(CdcPositionResetEventArgs? eventArgs)
	{
		if (eventArgs == null)
		{
			return "The Firestore CDC processor detected a stale position that cannot be recovered.";
		}

		var stalePositionStr = eventArgs.StalePosition != null
			? $"0x{Convert.ToHexString(eventArgs.StalePosition)}"
			: "unknown";
		var collectionInfo = !string.IsNullOrEmpty(eventArgs.CaptureInstance)
			? $" for collection '{eventArgs.CaptureInstance}'"
			: !string.IsNullOrEmpty(eventArgs.DatabaseName)
				? $" for project '{eventArgs.DatabaseName}'"
				: string.Empty;

		var grpcStatusInfo = string.Empty;
		if (eventArgs.AdditionalContext?.TryGetValue("GrpcStatusCode", out var grpcStatusObj) == true
			&& grpcStatusObj is int grpcStatus)
		{
			grpcStatusInfo = $" gRPC status {grpcStatus}.";
		}

		return $"Firestore CDC processor '{eventArgs.ProcessorId}' detected stale position{collectionInfo}. " +
			   $"Reason: {eventArgs.ReasonCode}.{grpcStatusInfo} Position: {stalePositionStr}. " +
			   $"Detected at: {eventArgs.DetectedAt:O}";
	}
}
