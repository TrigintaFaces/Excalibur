// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// The nested option groups on <see cref="DispatchOptions"/> are constructed, so a consumer can configure
/// them without a null check.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.DependencyInjection)]
public sealed class DispatchOptionsNestedOptionsShould
{
	[Fact]
	public void DispatchOptions_NestedProperties_AreInitialized()
	{
		var options = new DispatchOptions();
		_ = options.Inbox.ShouldNotBeNull();
		_ = options.Outbox.ShouldNotBeNull();
		_ = options.Consumer.ShouldNotBeNull();
		_ = options.CrossCutting.Performance.ShouldNotBeNull();
	}
}
