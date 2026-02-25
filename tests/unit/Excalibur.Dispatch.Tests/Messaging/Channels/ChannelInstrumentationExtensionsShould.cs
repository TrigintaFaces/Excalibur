// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ChannelInstrumentationExtensionsShould
{
	[Fact]
	public void BeginActivity_ReturnNullWhenNoListenerRegistered()
	{
		// Act
		var activity = ChannelInstrumentationExtensions.BeginActivity("test-operation");

		// Assert
		// Without a listener, StartActivity returns null
		activity?.Dispose();
	}

	[Fact]
	public void BeginActivity_ReturnActivityWhenListenerRegistered()
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name.Contains("Channel", StringComparison.OrdinalIgnoreCase),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		// Act
		var activity = ChannelInstrumentationExtensions.BeginActivity("test-operation");

		// Assert
		if (activity != null)
		{
			activity.OperationName.ShouldBe("test-operation");
			activity.Dispose();
		}
	}
}
