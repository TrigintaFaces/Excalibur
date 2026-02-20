// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// RabbitMQ message persistence levels.
/// </summary>
public enum RabbitMqPersistence
{
	/// <summary>
	/// Messages are not persisted and may be lost on broker restart.
	/// </summary>
	Transient = 0,

	/// <summary>
	/// Messages are persisted to disk and survive broker restarts.
	/// </summary>
	Persistent = 1,
}
