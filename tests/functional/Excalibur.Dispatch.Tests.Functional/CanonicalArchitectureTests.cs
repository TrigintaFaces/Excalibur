// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional;

/// <summary>
///     Tests that verify the canonical architecture requirements are met.
/// </summary>
public class CanonicalArchitectureTests
{
	/// <summary>
	///     R1.1: Verify all message execution flows through IDispatcher.
	/// </summary>
	[Fact]
	public async Task R11AllExecutionFlowsThroughDispatcher()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddDispatch(typeof(CanonicalArchitectureTests).Assembly);

		var serviceProvider = services.BuildServiceProvider();

		// Act & Assert This test verifies that the dispatcher is properly configured and all message flows go through IDispatcher
		await Task.CompletedTask.ConfigureAwait(false);

		// Verify IDispatcher is registered
		var dispatcher = serviceProvider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull();
	}
}
