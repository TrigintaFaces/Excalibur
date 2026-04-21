// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Provides administrative Google Pub/Sub metrics operations.
/// Implementations should implement this alongside <see cref="IGooglePubSubMetrics"/>.
/// </summary>
public interface IGooglePubSubMetricsAdmin
{
	/// <summary>Records batch completion.</summary>
	void BatchCompleted(int size, TimeSpan duration);

	/// <summary>Records connection creation.</summary>
	void ConnectionCreated();

	/// <summary>Records connection closure.</summary>
	void ConnectionClosed();

	/// <summary>Records flow control state.</summary>
	void RecordFlowControl(int permits, int bytes);
}
