// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests.Authorization;

/// <summary>
/// Handler registries for arms that are not about handler resolution.
/// </summary>
internal static class TestHandlerRegistry
{
	/// <summary>
	/// A registry that claims to know every message type, and to have no handler metadata for any of them.
	/// </summary>
	/// <remarks>
	/// The middleware refuses when it cannot determine the handlers, because an absence of metadata is not an
	/// absence of a requirement. Arms that are about something else — role evaluation, policy composition,
	/// what a denial discloses — must not trip that refusal, so they use a registry that ANSWERS. It answers
	/// with an empty handler set, which is the honest "determinable, and nothing is declared".
	/// </remarks>
	public static IHandlerRegistry KnowingEveryMessageType { get; } = new OmniscientButEmptyRegistry();

	/// <summary>
	/// Implemented directly against <see cref="IHandlerRegistry"/>, inheriting nothing that could supply the
	/// member under test — so an arm binds the interface's contract rather than a base class's convenience.
	/// </summary>
	private sealed class OmniscientButEmptyRegistry : IHandlerRegistry
	{
		public void Register(Type messageType, Type handlerType, bool expectsResponse, Type? responseType = null)
		{
		}

		public bool TryGetHandler(Type messageType, out IHandlerRegistryEntry entry)
		{
			entry = null!;
			return false;
		}

		public IReadOnlyList<IHandlerRegistryEntry> GetAll() => [];

		public bool TryGetHandlers(Type messageType, out IReadOnlyList<IHandlerRegistryEntry> entries)
		{
			entries = [new KnownButUndecorated(messageType)];
			return true;
		}

		private sealed class KnownButUndecorated(Type messageType) : IHandlerRegistryEntry
		{
			public Type MessageType { get; } = messageType;

			// A handler type carrying no [Authorize] and no [AllowAnonymous]: present, and declaring nothing.
			public Type HandlerType => typeof(KnownButUndecorated);

			public bool ExpectsResponse => false;

			public Type? ResponseType => null;
		}
	}
}
