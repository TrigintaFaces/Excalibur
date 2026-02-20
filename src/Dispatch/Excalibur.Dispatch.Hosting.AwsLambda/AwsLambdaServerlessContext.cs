// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Hosting.AwsLambda;

/// <summary>
/// AWS Lambda specific implementation of serverless context.
/// </summary>
public class AwsLambdaServerlessContext : ServerlessContextBase
{
#if AWS_LAMBDA_SUPPORT
	private readonly ILambdaContext _lambdaContext;
#endif

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsLambdaServerlessContext" /> class.
	/// </summary>
	/// <param name="lambdaContext"> The AWS Lambda context. </param>
	/// <param name="logger"> The logger instance. </param>
	public AwsLambdaServerlessContext(object lambdaContext, ILogger logger)
		: base(lambdaContext, ServerlessPlatform.AwsLambda, logger)
	{
#if AWS_LAMBDA_SUPPORT
		if (lambdaContext is ILambdaContext context)
		{
			_lambdaContext = context;
		}
		else
		{
			throw new ArgumentException(ErrorConstants.LambdaContextMustImplementILambdaContext, nameof(lambdaContext));
		}
#else
		throw new NotSupportedException(ErrorConstants.AwsLambdaSupportNotAvailable);
#endif
	}

	/// <inheritdoc />
	public override string RequestId =>
#if AWS_LAMBDA_SUPPORT
		_lambdaContext.AwsRequestId;
#else
		"unknown";

#endif

	/// <inheritdoc />
	public override string FunctionName =>
#if AWS_LAMBDA_SUPPORT
		_lambdaContext.FunctionName;
#else
		"unknown";

#endif

	/// <inheritdoc />
	public override string FunctionVersion =>
#if AWS_LAMBDA_SUPPORT
		_lambdaContext.FunctionVersion;
#else
		"$LATEST";

#endif

	/// <inheritdoc />
	public override string InvokedFunctionArn =>
#if AWS_LAMBDA_SUPPORT
		_lambdaContext.InvokedFunctionArn;
#else
		$"arn:aws:lambda:{Region}:000000000000:function:unknown";

#endif

	/// <inheritdoc />
	public override int MemoryLimitInMB =>
#if AWS_LAMBDA_SUPPORT
		_lambdaContext.MemoryLimitInMB;
#else
		128;

#endif

	/// <inheritdoc />
	public override string LogGroupName =>
#if AWS_LAMBDA_SUPPORT
		_lambdaContext.LogGroupName;
#else
		$"/aws/lambda/{FunctionName}";

#endif

	/// <inheritdoc />
	public override string LogStreamName =>
#if AWS_LAMBDA_SUPPORT
		_lambdaContext.LogStreamName;
#else
		$"{DateTimeOffset.UtcNow:yyyy/MM/dd}[LOCAL]";

#endif

	/// <inheritdoc />
	public override string CloudProvider => "AWS";

	/// <inheritdoc />
	public override string Region =>
		Environment.GetEnvironmentVariable("AWS_REGION") ??
		Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ??
		"us-east-1";

	/// <inheritdoc />
	public override string AccountId
	{
		get
		{
			// Extract account ID from the function ARN
			var arn = InvokedFunctionArn;
			if (!string.IsNullOrEmpty(arn))
			{
				var parts = arn.Split(':');
				if (parts.Length >= 5)
				{
					return parts[4];
				}
			}

			return "unknown";
		}
	}

	/// <inheritdoc />
	public override DateTimeOffset ExecutionDeadline
	{
		get
		{
#if AWS_LAMBDA_SUPPORT
			return DateTimeOffset.UtcNow.Add(_lambdaContext.RemainingTime);
#else
			return DateTimeOffset.UtcNow.AddMinutes(15);
#endif
		}
	}

	/// <summary>
	/// Gets the remaining execution time for the Lambda function.
	/// </summary>
	/// <value>
	/// The remaining execution time for the Lambda function.
	/// </value>
	public TimeSpan RemainingExecutionTime =>
#if AWS_LAMBDA_SUPPORT
		_lambdaContext.RemainingTime;
#else
		TimeSpan.FromMinutes(15);

#endif

#if AWS_LAMBDA_SUPPORT
	/// <summary>
	/// Gets the AWS-specific Lambda context.
	/// </summary>
	public ILambdaContext LambdaContext => _lambdaContext;
#endif

	/// <inheritdoc />
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			// AWS Lambda context doesn't require explicit disposal but we can perform any cleanup here if needed
		}

		base.Dispose(disposing);
	}
}

