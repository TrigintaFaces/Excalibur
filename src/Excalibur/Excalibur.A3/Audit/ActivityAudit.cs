// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.A3.Audit.Events;
using Excalibur.Dispatch;
using Excalibur.Domain;

using ApiException = Excalibur.Dispatch.ApiException;

namespace Excalibur.A3.Audit;

/// <summary>
/// Represents an audited activity, including details about the request, response, and context.
/// </summary>
/// <typeparam name="TRequest"> The type of the request. </typeparam>
/// <typeparam name="TResponse"> The type of the response. </typeparam>
[MessageName("Excalibur.A3.ActivityAudit")]
public class ActivityAudit<TRequest, TResponse> : IActivityAudited
{
	/// <summary>
	/// JSON options that skip Stream-typed properties during serialization.
	/// Replaces the dependency on <c>ExcaliburJsonSerializerOptions.IgnoreStream</c>
	/// from <c>Excalibur.Data</c>.
	/// </summary>
#pragma warning disable IL2026 // RequiresUnreferencedCode: JsonStringEnumConverter used for audit serialization only
#pragma warning disable IL3050 // RequiresDynamicCode: JsonStringEnumConverter used for audit serialization only
	private static readonly JsonSerializerOptions s_auditSerializerOptions = new()
	{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter() },
	};
#pragma warning restore IL3050
#pragma warning restore IL2026

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

		// A declared message name if the request has one, so renaming or moving the request type does
		// not split an audit trail into rows that no longer match the ones before it. The simple type
		// name stays the fallback: it is what every audit row written so far carries, and changing that
		// default would silently stop new rows matching the existing history.
		ActivityName = MessageNameHelper.GetDeclaredName(request.GetType()) ?? request.GetType().Name;
		ApplicationName = context.ApplicationName() ?? "Unknown";
		ClientAddress = context.ClientAddress();
		CorrelationId = context.CorrelationId() ?? Guid.Empty;
		Exception = null;
		Login = OrSystem(accessToken?.Login);
		Request = request;
		Response = default;
		StatusCode = 0;
		TenantId = context.TenantId();
		Timestamp = DateTimeOffset.UtcNow;
		UserId = OrSystem(accessToken?.UserId);
		UserName = OrSystem(accessToken?.FullName);
	}

	/// <summary>
	/// Names an action with no caller. An anonymous token reports an empty user id and full name rather
	/// than null, so testing for null alone would record the actor as the empty string — an identity a
	/// reader of the audit trail cannot distinguish from a missing field.
	/// </summary>
	/// <param name="value"> The identity value read from the caller's token. </param>
	/// <returns> The value, or "System" when the caller is absent or anonymous. </returns>
	private static string OrSystem(string? value) => string.IsNullOrWhiteSpace(value) ? "System" : value;

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
	string? IAuditResult.Exception
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
	string IAuditResult.Request
	{
		[UnconditionalSuppressMessage("Trimming", "IL2046",
			Justification = "The requirement is declared on this getter and reaches the consumer at AuditExcaliburBuilderExtensions.AddAudit, which registers the middleware that reads it. IAuditResult itself stays bare because ActivityAudited implements Request as a plain stored string with no reflection, so annotating the interface would mislabel it.")]
		[UnconditionalSuppressMessage("AOT", "IL3051",
			Justification = "The requirement is declared on this getter and reaches the consumer at AuditExcaliburBuilderExtensions.AddAudit, which registers the middleware that reads it. IAuditResult itself stays bare because ActivityAudited implements Request as a plain stored string with no reflection, so annotating the interface would mislabel it.")]
		[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
		[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
		get => JsonSerializer.Serialize(Request, s_auditSerializerOptions);
		init => _ = value;
	}

	/// <summary>
	/// Gets or sets the response object associated with the activity.
	/// </summary>
	/// <value> The response object, or <see langword="null" /> if not available. </value>
	public TResponse? Response { get; set; }

	/// <inheritdoc />
	string? IAuditResult.Response
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
