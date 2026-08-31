// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;

namespace Excalibur.Metapackages.Tests;

/// <summary>
/// Resolution lock for the AWS and Azure experience metapackages.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks.</b> <c>AddDispatchAws</c> and <c>AddDispatchAzure</c> register a message
/// bus whose constructor requires <see cref="IPayloadSerializer"/>, and neither call registered one.
/// A consumer following the one-line getting-started path therefore built a container in which the
/// transport it had just asked for could not be constructed.
/// </para>
/// <para>
/// <b>Why the failure mode matters.</b> The bus is registered through a factory delegate that resolves
/// the serializer inside its body, and <c>ValidateOnBuild</c> only walks descriptors that name an
/// implementation type. So the gap is invisible to container validation and surfaces on first resolve --
/// which, for a transport driven by a hosted service, is host start in whatever environment the host is
/// deployed to, not the developer's machine. The <c>Never_name_the_serializer_in_container_validation</c>
/// arms pin that escape route so it stays documented rather than rediscovered.
/// </para>
/// <para>
/// <b>Where the fix belongs.</b> In the metapackage, not the transport package. The transport packages
/// treat serialization as a consumer concern and are locked to that by their own tests; the metapackage
/// is the batteries-included bundle whose job is to make the one-line call work. Every registration it
/// contributes is <c>TryAdd</c>, so a consumer who registers their own serializer still wins.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> Each arm resolves a real service from a real provider built from the public entry
/// point. On unmodified sources <c>GetRequiredService&lt;IPayloadSerializer&gt;()</c> throws
/// <see cref="InvalidOperationException"/> (no service registered) and the arm is RED; with the
/// registration seated it returns an instance and the arm is GREEN. Nothing here asserts the presence of
/// a descriptor -- a descriptor present but unresolvable is the disease.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metapackages")]
public sealed class CloudTransportMetapackageSerializationShould : UnitTestBase
{
	private const string AwsRegion = "us-east-1";
	private const string AzureNamespace = "excalibur-lock.servicebus.windows.net";

	[Fact]
	public async Task Resolve_the_payload_serializer_the_aws_bundle_requires()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchAws(sqs => sqs.UseRegion(AwsRegion));

		await using var provider = services.BuildServiceProvider();

		IPayloadSerializer? serializer = null;
		Should.NotThrow(() => serializer = provider.GetRequiredService<IPayloadSerializer>());
		_ = serializer.ShouldNotBeNull(
			"AddDispatchAws registers a bus whose constructor takes IPayloadSerializer, so the bundle "
			+ "must supply one");
	}

	[Fact]
	public async Task Resolve_the_payload_serializer_the_azure_bundle_requires()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchAzure(sb => sb.FullyQualifiedNamespace(AzureNamespace));

		await using var provider = services.BuildServiceProvider();

		IPayloadSerializer? serializer = null;
		Should.NotThrow(() => serializer = provider.GetRequiredService<IPayloadSerializer>());
		_ = serializer.ShouldNotBeNull(
			"AddDispatchAzure registers a bus whose constructor takes IPayloadSerializer, so the bundle "
			+ "must supply one");
	}

	[Fact]
	public void Never_name_the_serializer_in_container_validation_aws() =>
		ValidationReportFor(static services => services.AddDispatchAws(sqs => sqs.UseRegion(AwsRegion)))
			.ShouldNotContain(
				nameof(IPayloadSerializer),
				Case.Sensitive,
				"the bus is built by a factory delegate, so ValidateOnBuild cannot see its serializer "
				+ "dependency -- turning validation on would not have shown a consumer this gap");

	[Fact]
	public void Never_name_the_serializer_in_container_validation_azure() =>
		ValidationReportFor(static services =>
				services.AddDispatchAzure(sb => sb.FullyQualifiedNamespace(AzureNamespace)))
			.ShouldNotContain(
				nameof(IPayloadSerializer),
				Case.Sensitive,
				"the bus is built by a factory delegate, so ValidateOnBuild cannot see its serializer "
				+ "dependency -- turning validation on would not have shown a consumer this gap");

	/// <summary>
	/// Builds the composition with <c>ValidateOnBuild</c> and returns everything validation had to say.
	/// </summary>
	/// <param name="compose">The metapackage entry point under test.</param>
	/// <returns>
	/// The validation failure text, or the empty string when validation found nothing. The two arms above
	/// assert a negative over this text; their positive control is the sibling resolve arm in this class,
	/// which proves on unmodified sources that the serializer gap is real and that validation stayed
	/// silent about it anyway.
	/// </returns>
	private static string ValidationReportFor(Action<IServiceCollection> compose)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		compose(services);

		try
		{
			services.BuildServiceProvider(
				new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }).Dispose();
			return string.Empty;
		}
		catch (AggregateException validation)
		{
			return validation.ToString();
		}
	}
}
