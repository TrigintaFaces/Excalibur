// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Outbox;

namespace Excalibur.Saga.Tests.Core.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaOutboxOptionsShould
{
	[Fact]
	public void HaveNullPublishDelegateByDefault()
	{
		// Act
		var options = new SagaOutboxOptions();

		// Assert
		options.PublishDelegate.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingPublishDelegate()
	{
		// Arrange
		var options = new SagaOutboxOptions();
		Func<IReadOnlyList<Excalibur.Dispatch.Abstractions.IDomainEvent>, string, CancellationToken, Task> del =
			(_, _, _) => Task.CompletedTask;

		// Act
		options.PublishDelegate = del;

		// Assert
		options.PublishDelegate.ShouldNotBeNull();
	}
}
