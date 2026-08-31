// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Excalibur.Dispatch")]
[assembly: InternalsVisibleTo("Excalibur.Domain")]
[assembly: InternalsVisibleTo("Excalibur.EventSourcing")]

// The keyed-default forwarding alias seam (KeyedDefaultAliasServiceCollectionExtensions) is internal
// because the exclusion it drives is only sound while nothing outside this assembly can present the
// IKeyedDefaultAliasForwarder marker. Its producers (event sourcing above, the inbox, and sagas)
// register aliases through it; the multi-tenancy gate is its one consumer, reading descriptors back to
// tell a forwarder from a real store registration.
[assembly: InternalsVisibleTo("Excalibur.Inbox")]
[assembly: InternalsVisibleTo("Excalibur.MultiTenancy")]
[assembly: InternalsVisibleTo("Excalibur.Saga")]

// The first-party test fixture (AggregateTestFixture) stamps contiguous aggregate
// versions on Given(...) events using the same internal IEventMetadataWriter seam the
// framework uses in AggregateRoot.RaiseEvent, so replayed histories satisfy the
// LoadFromHistory contiguity check without exposing a public version setter.
[assembly: InternalsVisibleTo("Excalibur.Testing")]
