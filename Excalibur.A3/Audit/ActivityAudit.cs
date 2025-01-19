using System.Diagnostics.CodeAnalysis;

using Excalibur.A3.Audit.Events;
using Excalibur.Data.Serialization;
using Excalibur.Domain;
using Excalibur.Exceptions;

using Newtonsoft.Json;

namespace Excalibur.A3.Audit;

/// <summary>
///     Represents an audited activity, including details about the request, response, and context.
/// </summary>
/// <typeparam name="TRequest"> The type of the request. </typeparam>
/// <typeparam name="TResponse"> The type of the response. </typeparam>
public class ActivityAudit<TRequest, TResponse> : IActivityAudited
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ActivityAudit{TRequest, TResponse}" /> class.
	/// </summary>
	/// <param name="context"> The activity context providing contextual information. </param>
	/// <param name="request"> The request object associated with the activity. </param>
	public ActivityAudit(IActivityContext context, [DisallowNull] TRequest request)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(request);

		var accessToken = context.AccessToken() ?? throw new InvalidOperationException("context AccessToken not found.");

		ActivityName = request.GetType().Name;
		ApplicationName = context.ApplicationName();
		ClientAddress = context.ClientAddress();
		CorrelationId = context.CorrelationId();
		Exception = null;
		Login = accessToken.Login;
		Request = request;
		Response = default;
		StatusCode = 0;
		TenantId = context.TenantId();
		Timestamp = DateTimeOffset.UtcNow;
		UserId = accessToken.UserId;
		UserName = accessToken.FullName;
	}

	/// <summary>
	///     Gets or sets the name of the activity.
	/// </summary>
	public string ActivityName { get; init; }

	/// <summary>
	///     Gets or sets the name of the application.
	/// </summary>
	public string ApplicationName { get; init; }

	/// <summary>
	///     Gets or sets the client address from which the activity originated.
	/// </summary>
	public string? ClientAddress { get; init; }

	/// <summary>
	///     Gets or sets the correlation ID associated with the activity.
	/// </summary>
	public Guid CorrelationId { get; init; }

	/// <summary>
	///     Gets or sets the exception encountered during the activity, if any.
	/// </summary>
	public Exception? Exception { get; set; }

	/// <inheritdoc />
	string? IActivityAudited.Exception
	{
		get => Exception?.Message;
		init => _ = value;
	}

	/// <summary>
	///     Gets or sets the login of the user performing the activity.
	/// </summary>
	public string? Login { get; init; }

	/// <summary>
	///     Gets the request object associated with the activity.
	/// </summary>
	public TRequest Request { get; protected set; }

	/// <inheritdoc />
	string IActivityAudited.Request
	{
		get => JsonConvert.SerializeObject(Request, ExcaliburNewtonsoftSerializerSettings.IgnoreStream);
		init => _ = value;
	}

	/// <summary>
	///     Gets the response object associated with the activity.
	/// </summary>
	public TResponse? Response { get; protected set; }

	/// <inheritdoc />
	string? IActivityAudited.Response
	{
		get => Response?.ToString();
		init => _ = value;
	}

	/// <summary>
	///     Gets or sets the status code of the activity result.
	/// </summary>
	public int StatusCode { get; set; }

	/// <summary>
	///     Gets or sets the tenant ID associated with the activity.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	///     Gets or sets the timestamp of when the activity occurred.
	/// </summary>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	///     Gets or sets the user ID of the person performing the activity.
	/// </summary>
	public string UserId { get; init; }

	/// <summary>
	///     Gets or sets the username of the person performing the activity.
	/// </summary>
	public string UserName { get; init; }

	/// <summary>
	///     Executes the specified activity and captures its result, audit information, and exceptions.
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
