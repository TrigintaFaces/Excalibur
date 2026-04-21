// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Security.Aws;

/// <summary>
/// Fluent builder interface for configuring AWS security services (Secrets Manager credential store).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BindConfiguration"/> is mutually exclusive with programmatic setters (last-wins).
/// </para>
/// </remarks>
public interface ISecurityAwsBuilder
{
	/// <summary>Sets the AWS region for Secrets Manager operations (e.g., "us-east-1").</summary>
	ISecurityAwsBuilder Region(string region);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	ISecurityAwsBuilder BindConfiguration(string sectionPath);
}
