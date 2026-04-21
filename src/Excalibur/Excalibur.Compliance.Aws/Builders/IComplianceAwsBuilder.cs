// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Aws;

/// <summary>
/// Fluent builder interface for configuring AWS KMS key management settings.
/// </summary>
/// <remarks>
/// <para>
/// Connection methods (<see cref="Region"/>, <see cref="ServiceUrl"/>) use last-wins semantics.
/// <see cref="BindConfiguration"/> is mutually exclusive with programmatic setters (last-wins).
/// </para>
/// </remarks>
public interface IComplianceAwsBuilder
{
	/// <summary>Sets the AWS region for KMS operations (e.g., "us-east-1").</summary>
	IComplianceAwsBuilder Region(string region);

	/// <summary>Enables FIPS 140-2 validated endpoints.</summary>
	IComplianceAwsBuilder UseFipsEndpoint(bool useFips = true);

	/// <summary>Sets the key alias prefix for Excalibur Dispatch keys.</summary>
	IComplianceAwsBuilder KeyAliasPrefix(string prefix);

	/// <summary>Sets the environment name used in key aliases (e.g., "dev", "staging", "prod").</summary>
	IComplianceAwsBuilder Environment(string environment);

	/// <summary>Sets a custom KMS service endpoint URL (e.g., for LocalStack or VPC endpoints).</summary>
	IComplianceAwsBuilder ServiceUrl(string serviceUrl);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IComplianceAwsBuilder BindConfiguration(string sectionPath);
}
