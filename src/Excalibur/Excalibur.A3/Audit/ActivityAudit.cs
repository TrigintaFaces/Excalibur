// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.A3.Audit.Events;
using Excalibur.Data.Serialization;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain;

using ApiException = Excalibur.Dispatch.Abstractions.ApiException;

namespace Excalibur.A3.Audit;

/// <summary>
/// Represents an audited activity, including details about the request, response, and context.
/// </summary>
/// <typeparam name="TRequest"> The type of the request. </typeparam>
/// <typeparam name="TResponse"> The type of the response. </typeparam>
public class ActivityAudit<TRequest, TResponse> : IActivityAudited
{
	private readonly Dictionary<string, object> _headers = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="ActivityAudit{TRequest, TResponse}" /> class.
	/// </summary>
	/// <param name="context"> The activity context providing contextual information. </param>
	/// <param name="request"> The request object associated with the activity. </param>
	public ActivityAudit(IActivityContext context, [DisallowNull] TRequest request)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(request);

		var accessToken = context.AccessToken();

		ActivityName = request.GetType().Name;
		ApplicationName = context.ApplicationName() ?? "Unknown";
		ClientAddress = context.ClientAddress();
		CorrelationId = context.CorrelationId() ?? Guid.Empty;
		Exception = null;
		Login = accessToken?.Login ?? "System";
		Request = request;
		Response = default;
		StatusCode = 0;
		TenantId = context.TenantId();
		Timestamp = DateTimeOffset.UtcNow;
		UserId = accessToken?.UserId ?? "System";
		UserName = accessToken?.FullName ?? "System";
	}

	/// <summary>
	/// Gets the unique identifier for this audit record as a GUID.
	/// </summary>
	/// <value> A unique identifier for this audit instance. </value>
	public Guid Id { get; init; } = Guid.NewGuid();

	/// <summary>
	/// Gets the unique identifier for this audit record as a string.
	/// </summary>
	/// <value> The string representation of the audit's unique identifier. </value>
	public string MessageId => Id.ToString();

	/// <summary>
	/// Gets the type identifier for this audit record.
	/// </summary>
	/// <value> The fully qualified type name of the audit. </value>
	public string MessageType => GetType().FullName ?? GetType().Name;

	/// <summary>
	/// Gets the kind of message this audit represents.
	/// </summary>
	/// <value> Always returns <see cref="MessageKinds.Event" /> for audit records. </value>
	public MessageKinds Kind { get; init; } = MessageKinds.Event;

	/// <summary>
	/// Gets the message headers.
	/// </summary>
	/// <value> A read-only dictionary containing the audit's metadata headers. </value>
	public IReadOnlyDictionary<string, object> Headers => new ReadOnlyDictionary<string, object>(_headers);

	/// <summary>
	/// Gets the name of the activity.
	/// </summary>
	/// <value> The name of the activity. </value>
	public string ActivityName { get; init; }

	/// <summary>
	/// Gets the name of the application.
	/// </summary>
	/// <value> The name of the application. </value>
	public string ApplicationName { get; init; }

	/// <summary>
	/// Gets the client address from which the activity originated.
	/// </summary>
	/// <value> The client address, or <see langword="null" /> if not available. </value>
	public string? ClientAddress { get; init; }

	/// <summary>
	/// Gets the correlation ID associated with the activity.
	/// </summary>
	/// <value> The correlation ID associated with the activity. </value>
	public Guid CorrelationId { get; init; }

	/// <summary>
	/// Gets or sets the exception encountered during the activity, if any.
	/// </summary>
	/// <value> The exception encountered, or <see langword="null" /> if no exception occurred. </value>
	public Exception? Exception { get; set; }

	/// <inheritdoc />
	string? IActivityAudited.Exception
	{
		get => Exception?.Message;
		init => _ = value;
	}

	/// <summary>
	/// Gets the login of the user performing the activity.
	/// </summary>
	/// <value> The login of the user, or <see langword="null" /> if not available. </value>
	public string? Login { get; init; }

	/// <summary>
	/// Gets or sets the request object associated with the activity.
	/// </summary>
	/// <value> The request object associated with the activity. </value>
	public TRequest Request { get; protected set; }

	/// <inheritdoc />
	string IActivityAudited.Request
	{
		[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
		[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
		get => JsonSerializer.Serialize(Request, ExcaliburJsonSerializerOptions.IgnoreStream);
		init => _ = value;
	}

	/// <summary>
	/// Gets or sets the response object associated with the activity.
	/// </summary>
	/// <value> The response object, or <see langword="null" /> if not available. </value>
	public TResponse? Response { get; set; }

	/// <inheritdoc />
	string? IActivityAudited.Response
	{
		get => Response?.ToString();
		init => _ = value;
	}

	/// <summary>
	/// Gets or sets the status code of the activity result.
	/// </summary>
	/// <value> The status code of the activity result. </value>
	public int StatusCode { get; set; }

	/// <summary>
	/// Gets the tenant ID associated with the activity.
	/// </summary>
	/// <value> The tenant ID, or <see langword="null" /> if not available. </value>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets or sets the timestamp of when the activity occurred.
	/// </summary>
	/// <value> The timestamp of when the activity occurred. </value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets the user ID of the person performing the activity.
	/// </summary>
	/// <value> The user ID of the person performing the activity. </value>
	public string UserId { get; init; }

	/// <summary>
	/// Gets the username of the person performing the activity.
	/// </summary>
	/// <value> The username of the person performing the activity. </value>
	public string UserName { get; init; }

	/// <inheritdoc />
	public string EventId => MessageId;

	/// <inheritdoc />
	public string AggregateId => UserId;

	/// <inheritdoc />
	public long Version { get; init; }

	/// <inheritdoc />
	public DateTimeOffset OccurredAt => Timestamp;

	/// <inheritdoc />
	public string EventType => MessageType;

	/// <inheritdoc />
	public IDictionary<string, object>? Metadata => _headers;

	/// <inheritdoc />
	public object Body => this;

	/// <inheritdoc />
	public IMessageFeatures Features { get; } = new DefaultMessageFeatures();

	/// <summary>
	/// Executes the specified activity and captures its result, audit information, and exceptions.
	/// </summary>
	/// <param name="activity"> The activity to execute. </param>
	/// <returns> The response produced by the activity. </returns>
	public async Task<TResponse?> DecorateAsync(Func<Task<TResponse>> activity)
	{
		ArgumentNullException.ThrowIfNull(activity);
		try
		{
			Response = await activity().ConfigureAwait(false);
			StatusCode = 200;

			return Response;
		}
		catch (ApiException ex)
		{
			Exception = ex;
			StatusCode = ex.StatusCode;

			throw;
		}
		catch (Exception ex)
		{
			Exception = ex;
			StatusCode = 500;

			throw;
		}
		finally
		{
			Timestamp = DateTimeOffset.UtcNow;
		}
	}
}
