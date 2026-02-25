// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

using Amazon.SQS.Model;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Represents a batch of messages to send.
/// </summary>
public sealed class SendMessageBatch
{
	/// <summary>
	/// Gets the entries to send.
	/// </summary>
	public Collection<SendMessageBatchRequestEntry> Entries { get; } = [];
}
