// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Claims;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Threading;

/// <summary>Copies supported data across an in-process background ownership boundary.</summary>
internal static class BackgroundContextSnapshot
{
	internal static MessageContext Capture(IDispatchMessage message, IMessageContext source)
	{
		if (System.Transactions.Transaction.Current is not null)
		{
			throw new InvalidOperationException("Background execution cannot detach from an active transaction. Use a durable outbox handoff.");
		}
		var target = new MessageContext
		{
			Message = message, MessageId = source.MessageId,
			CorrelationId = source.CorrelationId, CausationId = source.CausationId,
		};
		foreach (var item in source.Items)
		{
			target.Items[item.Key] = item.Value switch
			{
				null => null!,
				string or bool or char or byte or sbyte or short or ushort or int or uint or long or ulong
					or float or double or decimal or Guid or DateTime or DateTimeOffset or TimeSpan => item.Value,
				ClaimsPrincipal principal => new ClaimsPrincipal(principal.Identities.Select(static identity => CopyIdentity(identity, 0))),
				_ => throw new InvalidOperationException($"Background metadata '{item.Key}' is not a supported owned value. Use a durable message payload instead."),
			};
		}
		foreach (var feature in source.Features)
		{
			target.Features[feature.Key] = feature.Value switch
			{
				IMessageIdentityFeature identity => new MessageIdentityFeature
				{
					UserId = identity.UserId, TenantId = identity.TenantId, SessionId = identity.SessionId,
					WorkflowId = identity.WorkflowId, ExternalId = identity.ExternalId, TraceParent = identity.TraceParent,
				},
				IMessageRoutingFeature routing => new MessageRoutingFeature
				{
					Source = routing.Source, PartitionKey = routing.PartitionKey,
					RoutingDecision = routing.RoutingDecision is { } decision
						? decision with { Endpoints = decision.Endpoints.ToArray(), MatchedRules = decision.MatchedRules.ToArray() }
						: null,
				},
				IMessageProcessingFeature processing => new MessageProcessingFeature
				{
					ProcessingAttempts = processing.ProcessingAttempts, IsRetry = processing.IsRetry,
					FirstAttemptTime = processing.FirstAttemptTime, DeliveryCount = processing.DeliveryCount,
				},
				_ => throw new InvalidOperationException($"Background feature '{feature.Key.Name}' cannot cross the ownership boundary. Use a durable handoff or await dispatch."),
			};
		}
		return target;
	}
	private static ClaimsIdentity CopyIdentity(ClaimsIdentity identity, int depth)
	{
		if (depth >= 32 || identity.BootstrapContext is not null and not string)
		{
			throw new InvalidOperationException("Background identity contains unsupported delegation depth or mutable bootstrap metadata.");
		}
		return new ClaimsIdentity(identity.Claims.Select(static claim => claim.Clone()), identity.AuthenticationType,
			identity.NameClaimType, identity.RoleClaimType)
		{
			Label = identity.Label,
			BootstrapContext = identity.BootstrapContext,
			Actor = identity.Actor is { } actor ? CopyIdentity(actor, depth + 1) : null,
		};
	}

}
