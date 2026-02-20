// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Interface for messages that require activity-based authorization with optional resource-specific permissions.
/// </summary>
public interface IRequireActivityAuthorization : IRequireAuthorization
{
	/// <summary>
	/// Gets the resource this activity applies Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration.
	/// </summary>
	/// <value>The resource ID, or <see langword="null"/> if not available.</value>
	string? ResourceId { get; }

	/// <summary>
	/// Gets the type of resource to be authorized against.
	/// </summary>
	/// <value>The types of resource.</value>
	[JsonIgnore]
	string[] ResourceTypes { get; }
}
