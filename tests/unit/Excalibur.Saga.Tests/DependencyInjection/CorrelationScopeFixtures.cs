// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Tests.DependencyInjection
{
    public sealed class CorrelationSharedState : SagaState { public string Owner { get; set; } = string.Empty; }
}
namespace Excalibur.Saga.Tests.CorrelationScopeA
{
    internal sealed class OrderSaga(DependencyInjection.CorrelationSharedState state, IDispatcher dispatcher, ILogger logger)
        : SagaBase<DependencyInjection.CorrelationSharedState>(state, dispatcher, logger)
    {
        public override bool HandlesEvent(object eventMessage) => true;
        public override Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
        { State.Owner = "A"; return Task.FromResult(SagaEventOutcome.Handled); }
    }
}
namespace Excalibur.Saga.Tests.CorrelationScopeB
{
    internal sealed class OrderSaga(DependencyInjection.CorrelationSharedState state, IDispatcher dispatcher, ILogger logger)
        : SagaBase<DependencyInjection.CorrelationSharedState>(state, dispatcher, logger)
    {
        public override bool HandlesEvent(object eventMessage) => true;
        public override Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
        { State.Owner = "B"; return Task.FromResult(SagaEventOutcome.Handled); }
    }
}
