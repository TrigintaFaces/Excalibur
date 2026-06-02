// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;

namespace ProofOfLife.Domain.Events;

/// <summary>Event raised when a new todo is created.</summary>
public sealed record TodoCreated(Guid TodoId, string Title) : DomainEvent;

/// <summary>Event raised when a todo is marked as completed.</summary>
public sealed record TodoCompleted(Guid TodoId) : DomainEvent;

/// <summary>Event raised when a todo's title is updated.</summary>
public sealed record TodoTitleUpdated(Guid TodoId, string NewTitle) : DomainEvent;
