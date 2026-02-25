// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Marks a property to be populated from the current <see cref="IMessageContext" />.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MessageContextAttribute : Attribute;
