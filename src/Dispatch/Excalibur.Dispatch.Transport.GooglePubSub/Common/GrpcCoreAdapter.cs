// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Helper class for gRPC core adapter operations.
/// </summary>
public static class GrpcCoreAdapter
{
	/// <summary>
	/// Creates a new channel pool.
	/// </summary>
	/// <param name="address"> The target address. </param>
	/// <returns> A new single channel pool. </returns>
	public static SingleChannelPool Create(string address) => new(address);
}
