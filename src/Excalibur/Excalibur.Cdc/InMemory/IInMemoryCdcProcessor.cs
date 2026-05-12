// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.InMemory;

/// <summary>
/// Defines the contract for an in-memory CDC processor.
/// </summary>
/// <remarks>
/// <para>
/// This processor handles simulated CDC changes from an <see cref="IInMemoryCdcStore"/>
/// for testing scenarios. Extends <see cref="ICdcProcessor{TEvent}"/> for
/// provider-agnostic batch processing.
/// </para>
/// </remarks>
public interface IInMemoryCdcProcessor : ICdcProcessor<InMemoryCdcChange>;

