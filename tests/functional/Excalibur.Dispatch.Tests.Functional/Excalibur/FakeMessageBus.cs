// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.Excalibur;

/// <summary>
///     Fake message bus for testing.
/// </summary>
public class FakeMessageBus : DispatchIMessageBus
{
	private readonly List<object> publishedMessages = [];

	private readonly List<object> sentCommands = [];

	/// <summary>
	///     Gets the published messages.
	/// </summary>
	public IReadOnlyList<object> PublishedMessages => publishedMessages;

	/// <summary>
	///     Gets the sent commands.
	/// </summary>
	public IReadOnlyList<object> SentCommands => sentCommands;

	/// <summary>
	///     Publishes an action.
	/// </summary>
	public async Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		sentCommands.Add(action);
		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <summary>
	///     Publishes an event.
	/// </summary>
	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		publishedMessages.Add(evt);
		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <summary>
	///     Publishes a document.
	/// </summary>
	public async Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		publishedMessages.Add(doc);
		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <summary>
	///     Clears all recorded messages.
	/// </summary>
	public void Clear()
	{
		publishedMessages.Clear();
		sentCommands.Clear();
	}
}
