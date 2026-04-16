// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.S3;

namespace Excalibur.EventSourcing.AwsS3;

/// <summary>
/// Fluent builder for configuring AWS S3 cold event store settings.
/// </summary>
/// <remarks>
/// <para>
/// Connection methods (<see cref="ServiceUrl"/>, <see cref="Region"/>,
/// <see cref="Client(IAmazonS3)"/>, <see cref="ClientFactory"/>,
/// <see cref="BindConfiguration"/>) use last-wins semantics: setting one
/// clears the others.
/// </para>
/// <para>
/// Non-connection methods (<see cref="BucketName"/>, <see cref="KeyPrefix"/>)
/// are additive and can be combined with any connection method.
/// </para>
/// </remarks>
public interface IEventSourcingAwsS3Builder
{
	/// <summary>Sets the S3 bucket name for cold event storage.</summary>
	IEventSourcingAwsS3Builder BucketName(string bucketName);

	/// <summary>Sets the key prefix for archived event objects.</summary>
	IEventSourcingAwsS3Builder KeyPrefix(string keyPrefix);

	/// <summary>Sets the S3 service URL (for LocalStack or custom endpoints).</summary>
	IEventSourcingAwsS3Builder ServiceUrl(string serviceUrl);

	/// <summary>Sets the AWS region explicitly (as a string, e.g. "us-east-1").</summary>
	IEventSourcingAwsS3Builder Region(string region);

	/// <summary>Sets a pre-configured <see cref="IAmazonS3"/> client.</summary>
	IEventSourcingAwsS3Builder Client(IAmazonS3 client);

	/// <summary>Sets a factory that resolves an <see cref="IAmazonS3"/> from DI.</summary>
	IEventSourcingAwsS3Builder ClientFactory(Func<IServiceProvider, IAmazonS3> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IEventSourcingAwsS3Builder BindConfiguration(string sectionPath);
}
