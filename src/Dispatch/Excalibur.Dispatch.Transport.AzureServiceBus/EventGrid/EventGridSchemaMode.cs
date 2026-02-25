// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Specifies the event schema used for Azure Event Grid publishing.
/// </summary>
public enum EventGridSchemaMode
{
	/// <summary>
	/// CloudEvents v1.0 schema (recommended). Natively supported by Event Grid.
	/// </summary>
	CloudEvents,

	/// <summary>
	/// Azure Event Grid native schema.
	/// </summary>
	EventGridSchema,
}
