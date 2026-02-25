// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.A3.Events;

namespace Excalibur.A3.Audit.Events;

/// <summary>
/// Represents an audited activity, capturing details about an activity performed in the system, including metadata for auditing purposes.
/// </summary>
public sealed class ActivityAudited : DomainEventBase, IActivityAudited
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ActivityAudited" /> class by copying properties from an existing
	/// <see cref="IActivityAudited" /> instance.
	/// </summary>
	/// <param name="audit"> The <see cref="IActivityAudited" /> instance to copy data from. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="audit" /> is <c> null </c>. </exception>
	[RequiresUnreferencedCode("Copying properties from IActivityAudited which has properties marked with RequiresUnreferencedCode")]
	[RequiresDynamicCode("Copying properties from IActivityAudited which has properties marked with RequiresDynamicCode")]
	[SetsRequiredMembers]
	public ActivityAudited(IActivityAudited audit)
	{
		ArgumentNullException.ThrowIfNull(audit);

		ActivityName = audit.ActivityName;
		ApplicationName = audit.ApplicationName;
		ClientAddress = audit.ClientAddress;
		CorrelationId = audit.CorrelationId;
		Exception = audit.Exception;
		Login = audit.Login;
		Request = audit.Request;
		Response = audit.Response;
		StatusCode = audit.StatusCode;
		TenantId = audit.TenantId;
		Timestamp = audit.Timestamp;
		UserId = audit.UserId;
		UserName = audit.UserName;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ActivityAudited" /> class.
	/// </summary>
	public ActivityAudited()
	{
	}

	/// <inheritdoc />
	public required string ActivityName { get; init; }

	/// <inheritdoc />
	public required string ApplicationName { get; init; }

	/// <inheritdoc />
	public string? ClientAddress { get; init; }

	/// <inheritdoc />
	public Guid CorrelationId { get; init; }

	/// <inheritdoc />
	public string? Exception { get; init; }

	/// <inheritdoc />
	public string? Login { get; init; }

	/// <inheritdoc />
	/// <value>The serialized request string.</value>
	public required string Request
	{
		[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize&lt;TValue&gt;(TValue, JsonSerializerOptions)")]
		[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize&lt;TValue&gt;(TValue, JsonSerializerOptions)")]
		get;
		init;
	}

	/// <inheritdoc />
	public string? Response { get; init; }

	/// <inheritdoc />
	public int StatusCode { get; set; }

	/// <inheritdoc />
	public string? TenantId { get; init; }

	/// <inheritdoc />
	public new DateTimeOffset Timestamp { get; set; }

	/// <inheritdoc />
	public required string UserId { get; init; }

	/// <inheritdoc />
	public required string UserName { get; init; }
}
