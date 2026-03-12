// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

[Collection("MessageTypeCacheTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Priority", "1")]
public sealed class MessageTypeCacheFallbackShould
{
	[Fact]
	public void GetOrCreateMetadata_ForRepeatedFallbackType_ReturnsSameInstance()
	{
		var first = MessageTypeCache.GetOrCreateMetadata(typeof(FallbackOnlyMessage));
		var second = MessageTypeCache.GetOrCreateMetadata(typeof(FallbackOnlyMessage));

		ReferenceEquals(first, second).ShouldBeTrue();
	}

	private sealed class FallbackOnlyMessage : IDispatchAction;
}
