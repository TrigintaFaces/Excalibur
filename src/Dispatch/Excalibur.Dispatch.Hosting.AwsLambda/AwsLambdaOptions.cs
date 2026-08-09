// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.AwsLambda;

/// <summary>
/// Options for the AWS Lambda serverless host integration.
/// </summary>
/// <remarks>
/// The "am I running on AWS Lambda?" decision is <b>configuration</b>, not a runtime
/// environment read on the hot path. <see cref="ColdStartOptimizationEnabled"/> is defaulted once, at the
/// composition root, from the <c>AWS_LAMBDA_FUNCTION_NAME</c> environment variable (which AWS sets on every
/// Lambda invocation). Consumers/tests set it directly through the Options primitive rather than mutating
/// process environment variables.
/// </remarks>
internal sealed class AwsLambdaOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether AWS Lambda cold-start optimization is enabled. The default is
	/// computed at registration from the presence of the <c>AWS_LAMBDA_FUNCTION_NAME</c> environment variable
	/// (present when running inside the Lambda runtime).
	/// </summary>
	public bool ColdStartOptimizationEnabled { get; set; }
}
