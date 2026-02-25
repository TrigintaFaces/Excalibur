// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Factory for creating AOT-compatible JSON serializers with specific contexts.
/// </summary>
public static class AotJsonSerializerFactory
{
	/// <summary>
	/// Creates a serializer for core message types.
	/// </summary>
	public static AotJsonSerializer CreateCore()
		=> new(CoreMessageJsonContext.Default);

	/// <summary>
	/// Creates a serializer for CloudEvents.
	/// </summary>
	public static AotJsonSerializer CreateForCloudEvents()
		=> new(CloudEventJsonContext.Default);
}
