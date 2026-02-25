// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using Excalibur.Dispatch.Abstractions;

namespace Tests.Shared.TestTypes;

/// <summary>
/// Simple test action for creating MessageContext instances in tests.
/// This is a minimal implementation of IDispatchAction suitable for test scenarios
/// where a concrete message type is needed to construct message contexts.
/// </summary>
public sealed class TestDispatchAction : IDispatchAction
{
}
