// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Defines the contract for ordering key message processing.
/// </summary>
public interface IOrderingKeyProcessor : IAsyncDisposable
{
	/// <summary>
	/// Gets statistics about ordering key processing.
	/// </summary>
	/// <returns> Processing statistics. </returns>
	OrderingKeyStatistics GetStatistics();
}
