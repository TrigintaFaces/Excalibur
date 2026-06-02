// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.AwsLambda;

/// <summary>
/// AWS Lambda specific implementation of serverless context.
/// </summary>
internal sealed class AwsLambdaServerlessContext : ServerlessContextBase
{
	private readonly ILambdaContext _lambdaContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsLambdaServerlessContext" /> class.
	/// </summary>
	/// <param name="lambdaContext"> The AWS Lambda context. </param>
	/// <param name="logger"> The logger instance. </param>
	public AwsLambdaServerlessContext(object lambdaContext, ILogger logger)
		: base(lambdaContext, ServerlessPlatform.AwsLambda, logger)
	{
		if (lambdaContext is ILambdaContext context)
		{
			_lambdaContext = context;
		}
		else
		{
			throw new ArgumentException(ErrorConstants.LambdaContextMustImplementILambdaContext, nameof(lambdaContext));
		}
	}

	/// <inheritdoc />
	public override string RequestId => _lambdaContext.AwsRequestId;

	/// <inheritdoc />
	public override string FunctionName => _lambdaContext.FunctionName;

	/// <inheritdoc />
	public override string FunctionVersion => _lambdaContext.FunctionVersion;

	/// <inheritdoc />
	public override string InvokedFunctionArn => _lambdaContext.InvokedFunctionArn;

	/// <inheritdoc />
	public override int MemoryLimitInMB => _lambdaContext.MemoryLimitInMB;

	/// <inheritdoc />
	public override string LogGroupName => _lambdaContext.LogGroupName;

	/// <inheritdoc />
	public override string LogStreamName => _lambdaContext.LogStreamName;

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
	public override DateTimeOffset ExecutionDeadline =>
		DateTimeOffset.UtcNow.Add(_lambdaContext.RemainingTime);

	/// <summary>
	/// Gets the remaining execution time for the Lambda function.
	/// </summary>
	/// <value>
	/// The remaining execution time for the Lambda function.
	/// </value>
	public TimeSpan RemainingExecutionTime => _lambdaContext.RemainingTime;

	/// <summary>
	/// Gets the AWS-specific Lambda context.
	/// </summary>
	public ILambdaContext LambdaContext => _lambdaContext;

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
