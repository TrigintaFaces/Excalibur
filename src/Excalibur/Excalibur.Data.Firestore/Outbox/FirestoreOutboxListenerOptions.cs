// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Firestore.Outbox;

/// <summary>
/// Configuration options for the Firestore outbox real-time listener.
/// </summary>
public sealed class FirestoreOutboxListenerOptions
{
	/// <summary>
	/// Gets or sets the Firestore collection path for outbox messages.
	/// </summary>
	/// <value>The collection path. Defaults to "outbox_messages".</value>
	[Required]
	public string CollectionPath { get; set; } = "outbox_messages";

	/// <summary>
	/// Gets or sets the fallback poll interval when the snapshot listener is unavailable.
	/// </summary>
	/// <value>The poll interval. Defaults to 5 seconds.</value>
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the maximum number of messages to process per listener notification.
	/// </summary>
	/// <value>The maximum batch size. Defaults to 100.</value>
	[Range(1, 1000)]
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically restart the listener on error.
	/// </summary>
	/// <value><see langword="true"/> to auto-restart; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool AutoRestartOnError { get; set; } = true;

	/// <summary>
	/// Gets or sets the delay before restarting the listener after an error.
	/// </summary>
	/// <value>The restart delay. Defaults to 10 seconds.</value>
	public TimeSpan RestartDelay { get; set; } = TimeSpan.FromSeconds(10);
}
