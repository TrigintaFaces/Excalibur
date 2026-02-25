// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Default implementation of result cache policy that uses a delegate function to determine caching behavior. Provides flexible caching
/// logic based on message and result content.
/// </summary>
/// <param name="policy"> The delegate function that determines whether to cache a specific message result. </param>
public sealed class DefaultResultCachePolicy(Func<IDispatchMessage, object?, bool> policy) : IResultCachePolicy
{
	/// <inheritdoc />
	public bool ShouldCache(IDispatchMessage message, object? result) => policy(message, result);
}
