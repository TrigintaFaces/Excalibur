// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents a transport registration.
/// </summary>
/// <param name="Adapter"> The transport adapter. </param>
/// <param name="TransportType"> The transport type. </param>
/// <param name="Options"> The transport options. </param>
public sealed record TransportRegistration(
	ITransportAdapter Adapter,
	string TransportType,
	Dictionary<string, object> Options);
