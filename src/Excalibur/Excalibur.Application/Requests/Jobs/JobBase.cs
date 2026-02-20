// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Application.Requests.Jobs;

/// <summary>
/// Represents the base implementation for a job in the system.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="JobBase" /> class with the specified correlation ID and tenant ID. </remarks>
/// <param name="correlationId"> The correlation ID for the job. </param>
/// <param name="tenantId"> The tenant ID associated with the job. Defaults to TenantDefaults.DefaultTenantId if not provided. </param>
public abstract class JobBase(Guid correlationId, string? tenantId = null) : IJob
{
	private readonly Dictionary<string, object> _headers = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="JobBase" /> class with default values.
	/// </summary>
	protected JobBase()
		: this(Guid.Empty)
	{
	}

	/// <summary>
	/// Gets the unique identifier for this job as a GUID.
	/// </summary>
	/// <value> A unique identifier for this job instance. </value>
	public Guid Id { get; protected init; } = Guid.NewGuid();

	/// <summary>
	/// Gets the unique identifier for this job as a string.
	/// </summary>
	/// <value> The string representation of the job's unique identifier. </value>
	public string MessageId => Id.ToString();

	/// <summary>
	/// Gets the type identifier for this job.
	/// </summary>
	/// <value> The fully qualified type name of the job. </value>
	public string MessageType => GetType().FullName ?? GetType().Name;

	/// <summary>
	/// Gets the kind of message this job represents.
	/// </summary>
	/// <value> Always returns <see cref="MessageKinds.Action" /> for jobs. </value>
	public MessageKinds Kind { get; protected init; } = MessageKinds.Action;

	/// <summary>
	/// Gets the message headers.
	/// </summary>
	/// <value> A read-only dictionary containing the job's metadata headers. </value>
	public IReadOnlyDictionary<string, object> Headers => new ReadOnlyDictionary<string, object>(_headers);

	/// <inheritdoc />
	public ActivityType ActivityType => ActivityType.Job;

	/// <inheritdoc />
	public string ActivityName => ActivityNameConvention.ResolveName(GetType());

	/// <inheritdoc />
	public virtual string ActivityDisplayName => ActivityNameConvention.ResolveDisplayName(GetType());

	/// <inheritdoc />
	public virtual string ActivityDescription => ActivityNameConvention.ResolveDescription(GetType());

	/// <inheritdoc />
	public Guid CorrelationId { get; protected init; } = correlationId;

	/// <inheritdoc />
	public string? TenantId { get; protected init; } = tenantId ?? TenantDefaults.DefaultTenantId;
}
