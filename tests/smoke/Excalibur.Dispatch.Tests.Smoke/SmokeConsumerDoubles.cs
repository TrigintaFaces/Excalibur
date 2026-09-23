// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using CloudNative.CloudEvents;

using Excalibur.Caching.Projections;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Tests.Smoke;

// The doubles a consumer would supply for the generic entry points in PackageDiSmokeTests.
//
// Several public Add*() entry points take a consumer implementation as a TYPE ARGUMENT rather than
// resolving one from the container, so there is nothing for a stub factory to stand in for: without a
// concrete type the call does not compile, and the entry point therefore sits outside the smoke gate.
// These are the smallest types that satisfy those constraints.
//
// Every member throws. The container CONSTRUCTS these under ValidateOnBuild -- which is the property
// under test -- and nothing ever calls them, so a member that did run is a harness defect, not a
// silent substitute for infrastructure the consumer owns.

/// <summary>A transport message type for <c>AddCloudEventEncoder</c> to close its generic over.</summary>
internal sealed class SmokeTransportMessage;

/// <summary>A consumer-supplied CloudEvent encoder.</summary>
internal sealed class SmokeCloudEventEncoder : ICloudEventEncoder<SmokeTransportMessage>
{
	public CloudEventOptions Options => throw new NotSupportedException(NotResolved);

	[RequiresUnreferencedCode("Never invoked; the harness only constructs this double.")]
	[RequiresDynamicCode("Never invoked; the harness only constructs this double.")]
	public Task<SmokeTransportMessage> ToTransportMessageAsync(
		CloudEvent cloudEvent,
		CloudEventMode mode,
		CancellationToken cancellationToken)
		=> throw new NotSupportedException(NotResolved);

	private const string NotResolved =
		"SmokeCloudEventEncoder is a DI smoke double. It exists to be constructed, never to encode.";
}

/// <summary>A consumer-supplied Pub/Sub schema manager. The framework ships no implementation.</summary>
internal sealed class SmokePubSubSchemaManager : IPubSubSchemaManager
{
	public Task<PubSubSchemaInfo> CreateSchemaAsync(
		string schemaId,
		string definition,
		string schemaType,
		CancellationToken cancellationToken)
		=> throw new NotSupportedException(NotResolved);

	public Task<PubSubSchemaInfo> UpdateSchemaAsync(
		string schemaId,
		string definition,
		CancellationToken cancellationToken)
		=> throw new NotSupportedException(NotResolved);

	public Task<SchemaValidationResult> ValidateSchemaAsync(
		string definition,
		string schemaType,
		CancellationToken cancellationToken)
		=> throw new NotSupportedException(NotResolved);

	private const string NotResolved =
		"SmokePubSubSchemaManager is a DI smoke double. It exists to be constructed, never to manage schemas.";
}

/// <summary>A consumer-supplied Azure Service Bus transaction.</summary>
internal sealed class SmokeAzureServiceBusTransaction : IAzureServiceBusTransaction
{
	public Task BeginTransactionAsync(CancellationToken cancellationToken)
		=> throw new NotSupportedException(NotResolved);

	public Task CommitAsync(CancellationToken cancellationToken)
		=> throw new NotSupportedException(NotResolved);

	public Task RollbackAsync(CancellationToken cancellationToken)
		=> throw new NotSupportedException(NotResolved);

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	private const string NotResolved =
		"SmokeAzureServiceBusTransaction is a DI smoke double. It exists to be constructed, never to transact.";
}

/// <summary>The projection type the DynamoDB and Firestore projection-store entry points close over.</summary>
internal sealed class SmokeProjection
{
	public string Id { get; set; } = string.Empty;
}

/// <summary>The message a projection tag resolver resolves tags for.</summary>
internal sealed class SmokeProjectionMessage;

/// <summary>
/// A consumer-supplied projection tag resolver, discovered by <c>WithProjectionResolvers</c> both by
/// explicit type and by assembly scan.
/// </summary>
internal sealed class SmokeProjectionTagResolver : IProjectionTagResolver<SmokeProjectionMessage>
{
	public IEnumerable<string> GetTags(SmokeProjectionMessage message)
		=> throw new NotSupportedException(
			"SmokeProjectionTagResolver is a DI smoke double. It exists to be constructed, never to resolve tags.");
}
