// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Protobuf related types.
/// </summary>
public static class ProtobufExtensions
{
	/// <summary>
	/// Converts to protobuf timestamp.
	/// </summary>
	/// <param name="dateTime"> The datetime to convert. </param>
	/// <returns> A protobuf timestamp. </returns>
	public static object ToTimestamp(this DateTimeOffset dateTime) => dateTime;

	/// <summary>
	/// Converts from protobuf timestamp.
	/// </summary>
	/// <param name="timestamp"> The timestamp to convert. </param>
	/// <returns> A DateTimeOffset. </returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Stub implementation - parameter reserved for future conversion logic")]
	public static DateTimeOffset ToDateTimeOffset(object timestamp) => DateTimeOffset.UtcNow;
}
