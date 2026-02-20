// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Serialization;

/// <summary>
/// Options for registering message serializers by version.
/// </summary>
public sealed class MessageSerializerOptions
{
	/// <summary>
	/// Gets mapping between serializer version and the serializer implementation type.
	/// </summary>
	/// <value>The current <see cref="SerializerMap"/> value.</value>
	public Dictionary<int, Type> SerializerMap { get; } = [];
}
