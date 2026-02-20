// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Outbox.Health;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Outbox.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxServiceShould2
{
	[Fact]
	public void ThrowWhenInboxIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InboxService(null!, NullLogger<InboxService>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var inbox = A.Fake<IInbox>();
		Should.Throw<ArgumentNullException>(() =>
			new InboxService(inbox, null!));
	}

	[Fact]
	public async Task StartAndStopGracefully()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		A.CallTo(() => inbox.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(0));

		var service = new InboxService(
			inbox,
			NullLogger<InboxService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(200);
		await service.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => inbox.DisposeAsync())
			.MustHaveHappened();
	}

	[Fact]
	public async Task UseHealthStateWhenProvided()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var healthState = new BackgroundServiceHealthState();

		A.CallTo(() => inbox.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(0));

		var service = new InboxService(
			inbox,
			NullLogger<InboxService>.Instance,
			healthState);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(200);
		await service.StopAsync(CancellationToken.None);

		// Assert - no exception
	}

	[Fact]
	public async Task AcceptCustomDrainTimeout()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		A.CallTo(() => inbox.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(0));

		var service = new InboxService(
			inbox,
			NullLogger<InboxService>.Instance,
			drainTimeoutSeconds: 5);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(200);
		await service.StopAsync(CancellationToken.None);

		// Assert - no exception
	}
}
