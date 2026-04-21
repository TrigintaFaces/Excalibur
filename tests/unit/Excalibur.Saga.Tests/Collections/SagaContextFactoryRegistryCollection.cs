// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Saga.Tests.Collections;

/// <summary>
/// Serializes test classes that touch <c>SagaContextFactoryRegistry</c> static state.
/// </summary>
/// <remarks>
/// Per S807 CRUCIBLE diagnosis (msg 2147) and COMPASS Option B ruling (msg 2148): the
/// <c>SagaContextFactoryRegistry._frozen</c> static flag is mutated explicitly by
/// <see cref="Excalibur.Saga.Tests.StateMachine.SagaContextFactoryRegistryShould"/> and
/// implicitly (via <see cref="Excalibur.Saga.StateMachine.StateDefinition{TData}.When{TMessage}"/>)
/// by <see cref="Excalibur.Saga.Tests.StateMachine.StateDefinitionShould"/> and
/// <see cref="Excalibur.Saga.Tests.StateMachine.ProcessManagerShould"/>, producing a test-execution
/// race where a concurrent <c>Register</c> can observe <c>_frozen==true</c> and throw.
/// This collection definition serializes the 4 test classes that touch the registry; the rest of
/// the test assembly continues to parallelize normally.
/// </remarks>
[CollectionDefinition("SagaContextFactoryRegistry", DisableParallelization = true)]
public sealed class SagaContextFactoryRegistryCollection { }
