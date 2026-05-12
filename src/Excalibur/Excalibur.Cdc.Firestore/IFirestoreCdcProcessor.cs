// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Firestore;

/// <summary>
/// Defines the contract for processing Firestore CDC events via Realtime Listeners.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="ICdcStreamProcessor{TEvent, TPosition}"/>
/// with Firestore-specific types. All streaming and batch processing methods
/// are inherited from the base interfaces.
/// </para>
/// <para>
/// <b>Important:</b> Due to Firestore Realtime Listener semantics,
/// <see cref="ICdcProcessor{TEvent}.ProcessBatchAsync"/> can only reliably detect
/// Modified events. Added and Removed events may not be captured when using batch
/// processing mode. Use <see cref="ICdcStreamProcessor{TEvent, TPosition}.StartAsync"/>
/// for full change type support.
/// </para>
/// </remarks>
public interface IFirestoreCdcProcessor : ICdcStreamProcessor<FirestoreDataChangeEvent, FirestoreCdcPosition>;
