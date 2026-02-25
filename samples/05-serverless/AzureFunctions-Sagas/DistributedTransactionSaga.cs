using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Excalibur.Hosting.AzureFunctions;

namespace examples.Excalibur.Dispatch.Examples.Serverless.Azure.Sagas;

/// <summary>
/// Example of a distributed transaction saga that coordinates across multiple services.
/// Demonstrates complex saga patterns including parallel execution and conditional compensation.
/// </summary>
public class DistributedTransactionSagaFunction {
 private readonly ILogger<DistributedTransactionSagaFunction> _logger;

 /// <summary>
 /// Initializes a new instance of the <see cref="DistributedTransactionSagaFunction"/> class.
 /// </summary>
 public DistributedTransactionSagaFunction(ILogger<DistributedTransactionSagaFunction> logger)
 {
 _logger = logger;
 }

 /// <summary>
 /// Runs a distributed transaction saga across multiple services.
 /// </summary>
 [Function("DistributedTransactionSaga")]
 public async Task<DistributedTransactionResult> RunDistributedTransactionSaga(
 [OrchestrationTrigger] TaskOrchestrationContext context)
 {
 var input = context.GetInput<DistributedTransactionRequest>()!;

 // Build a complex saga with conditional steps and parallel execution
 var saga = SagaBuilder<DistributedTransactionRequest, DistributedTransactionResult>
 .Create("DistributedTransaction")
 .WithTimeout(TimeSpan.FromMinutes(10))
 .WithAutoCompensation(true)
 .WithDefaultRetry(maxAttempts: 3, firstRetryInterval: TimeSpan.FromSeconds(1))
 .WithInputValidation(async request =>
 {
 if (request.Services == null || !request.Services.Any())
 throw new ArgumentException("At least one service must be specified");

 if (request.TransactionData == null || !request.TransactionData.Any())
 throw new ArgumentException("Transaction data is required");

 await Task.CompletedTask;
 })

 // Step 1: Begin distributed transaction
 .AddStep<BeginTransactionRequest, BeginTransactionResult>("BeginTransaction", step => step
 .ExecuteActivity("BeginDistributedTransactionActivity")
 .WithInput((request, state) => new BeginTransactionRequest
 {
 TransactionId = state.SagaId,
 Services = request.Services,
 IsolationLevel = request.IsolationLevel,
 Timeout = TimeSpan.FromMinutes(5)
 })
 .WithOutput((result, state) =>
 {
 state.CustomData["GlobalTransactionId"] = result.GlobalTransactionId;
 state.CustomData["CoordinatorEndpoint"] = result.CoordinatorEndpoint;
 })
 .WithCompensation("AbortDistributedTransactionActivity"))

 // Step 2: Prepare services in parallel
 .AddParallelSteps("PrepareServices", group =>
 {
 group.AddStep<PrepareServiceRequest, PrepareServiceResult>("PrepareServiceA", step => step
 .ExecuteActivity("PrepareServiceActivity")
 .WithInput((request, state) => new PrepareServiceRequest
 {
 ServiceName = "ServiceA",
 GlobalTransactionId = (string)state.CustomData["GlobalTransactionId"],
 Data = request.TransactionData.GetValueOrDefault("ServiceA", new Dictionary<string, object>())
 })
 .WithOutput((result, state) =>
 state.CustomData[$"ServiceA_PrepareResult"] = result)
 .WithCompensation("RollbackServiceActivity"));

 group.AddStep<PrepareServiceRequest, PrepareServiceResult>("PrepareServiceB", step => step
 .ExecuteActivity("PrepareServiceActivity")
 .WithInput((request, state) => new PrepareServiceRequest
 {
 ServiceName = "ServiceB",
 GlobalTransactionId = (string)state.CustomData["GlobalTransactionId"],
 Data = request.TransactionData.GetValueOrDefault("ServiceB", new Dictionary<string, object>())
 })
 .WithOutput((result, state) =>
 state.CustomData[$"ServiceB_PrepareResult"] = result)
 .WithCompensation("RollbackServiceActivity"));

 group.AddStep<PrepareServiceRequest, PrepareServiceResult>("PrepareServiceC", step => step
 .ExecuteActivity("PrepareServiceActivity")
 .WithInput((request, state) => new PrepareServiceRequest
 {
 ServiceName = "ServiceC",
 GlobalTransactionId = (string)state.CustomData["GlobalTransactionId"],
 Data = request.TransactionData.GetValueOrDefault("ServiceC", new Dictionary<string, object>())
 })
 .WithOutput((result, state) =>
 state.CustomData[$"ServiceC_PrepareResult"] = result)
 .WithCompensation("RollbackServiceActivity"));
 })

 // Step 3: Validate all services prepared successfully
 .AddStep<ValidatePreparationRequest, ValidatePreparationResult>("ValidatePreparation", step => step
 .ExecuteActivity("ValidatePreparationActivity")
 .WithInput((request, state) => new ValidatePreparationRequest
 {
 GlobalTransactionId = (string)state.CustomData["GlobalTransactionId"],
 ServiceResults = new[]
 {
 state.CustomData["ServiceA_PrepareResult"] as PrepareServiceResult,
 state.CustomData["ServiceB_PrepareResult"] as PrepareServiceResult,
 state.CustomData["ServiceC_PrepareResult"] as PrepareServiceResult
 }.Where(r => r != null).ToList()
 })
 .WithOutput((result, state) =>
 {
 state.CustomData["AllServicesPrepared"] = result.AllPrepared;
 if (!result.AllPrepared)
 {
 throw new InvalidOperationException($"Not all services prepared successfully: {string.Join(", ", result.FailedServices)}");
 }
 }))

 // Step 4: Commit services in order (with dependencies)
 .AddStep<CommitServiceRequest, CommitServiceResult>("CommitServiceA", step => step
 .ExecuteActivity("CommitServiceActivity")
 .WithInput((request, state) => new CommitServiceRequest
 {
 ServiceName = "ServiceA",
 GlobalTransactionId = (string)state.CustomData["GlobalTransactionId"],
 PrepareToken = (state.CustomData["ServiceA_PrepareResult"] as PrepareServiceResult)?.PrepareToken
 })
 .WithOutput((result, state) => state.CustomData["ServiceA_CommitResult"] = result)
 .WithCompensation("CompensateServiceActivity"))

 .AddConditionalStep<CommitServiceRequest, CommitServiceResult>(
 "CommitServiceB",
 (request, state) => request.Services.Contains("ServiceB"),
 step => step
 .ExecuteActivity("CommitServiceActivity")
 .WithInput((request, state) => new CommitServiceRequest
 {
 ServiceName = "ServiceB",
 GlobalTransactionId = (string)state.CustomData["GlobalTransactionId"],
 PrepareToken = (state.CustomData["ServiceB_PrepareResult"] as PrepareServiceResult)?.PrepareToken,
 DependsOn = new[] { "ServiceA" }
 })
 .WithOutput((result, state) => state.CustomData["ServiceB_CommitResult"] = result)
 .WithCompensation("CompensateServiceActivity"))

 .AddConditionalStep<CommitServiceRequest, CommitServiceResult>(
 "CommitServiceC",
 (request, state) => request.Services.Contains("ServiceC"),
 step => step
 .ExecuteActivity("CommitServiceActivity")
 .WithInput((request, state) => new CommitServiceRequest
 {
 ServiceName = "ServiceC",
 GlobalTransactionId = (string)state.CustomData["GlobalTransactionId"],
 PrepareToken = (state.CustomData["ServiceC_PrepareResult"] as PrepareServiceResult)?.PrepareToken,
 DependsOn = new[] { "ServiceA", "ServiceB" }
 })
 .WithOutput((result, state) => state.CustomData["ServiceC_CommitResult"] = result)
 .WithCompensation("CompensateServiceActivity"))

 // Step 5: Complete distributed transaction
 .AddStep<CompleteTransactionRequest, CompleteTransactionResult>("CompleteTransaction", step => step
 .ExecuteActivity("CompleteDistributedTransactionActivity")
 .WithInput((request, state) => new CompleteTransactionRequest
 {
 GlobalTransactionId = (string)state.CustomData["GlobalTransactionId"],
 Status = TransactionStatus.Committed,
 CommitResults = state.CustomData
 .Where(kvp => kvp.Key.EndsWith("_CommitResult"))
 .Select(kvp => kvp.Value as CommitServiceResult)
 .Where(r => r != null)
 .ToList()
 })
 .WithOutput((result, state) => state.CustomData["TransactionResult"] = result))

 // Step 6: Audit trail
 .AddStep<AuditRequest, bool>("RecordAuditTrail", step => step
 .ExecuteActivity("RecordTransactionAuditActivity")
 .WithInput((request, state) => new AuditRequest
 {
 TransactionId = state.SagaId,
 GlobalTransactionId = (string)state.CustomData["GlobalTransactionId"],
 Services = request.Services,
 Status = "Completed",
 Duration = state.Duration,
 Steps = state.ExecutedSteps.Select(s => new AuditStep
 {
 StepName = s.StepName,
 Status = s.Status.ToString(),
 StartTime = s.StartTime,
 EndTime = s.EndTime,
 Duration = s.Duration
 }).ToList()
 }))

 .WithOutputBuilder(async (request, state) =>
 {
 var transactionResult = state.CustomData["TransactionResult"] as CompleteTransactionResult;

 return await Task.FromResult(new DistributedTransactionResult
 {
 TransactionId = state.SagaId,
 GlobalTransactionId = (string)state.CustomData["GlobalTransactionId"],
 Status = "Committed",
 Services = request.Services,
 CommittedAt = transactionResult?.CompletedAt ?? DateTime.UtcNow,
 Duration = state.Duration,
 ServiceResults = state.CustomData
 .Where(kvp => kvp.Key.EndsWith("_CommitResult"))
 .ToDictionary(
 kvp => kvp.Key.Replace("_CommitResult", ""),
 kvp => (kvp.Value as CommitServiceResult)?.IsSuccessful ?? false)
 });
 })

 .WithErrorHandler((state, ex) =>
 {
 _logger.LogError(ex, "Distributed transaction failed at step {Step}",
 state.LastExecutedStep?.StepName);

 // Record failure audit
 var _ = Task.Run(async () =>
 {
 try
 {
 await Task.Delay(100); // Ensure async
 _logger.LogWarning("Recording failed transaction audit for {TransactionId}", state.SagaId);
 }
 catch
 {
 // Best effort
 }
 });
 })

 .Build(_logger as ILogger<FluentSagaOrchestration<DistributedTransactionRequest, DistributedTransactionResult>>);

 return await saga.RunAsync(context, input);
 }
}

// Models for Distributed Transaction

public class DistributedTransactionRequest {
 public List<string> Services { get; set; } = new();
 public Dictionary<string, Dictionary<string, object>> TransactionData { get; set; } = new();
 public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;
 public Dictionary<string, string> Metadata { get; set; } = new();
}

public class DistributedTransactionResult {
 public string TransactionId { get; set; } = string.Empty;
 public string GlobalTransactionId { get; set; } = string.Empty;
 public string Status { get; set; } = string.Empty;
 public List<string> Services { get; set; } = new();
 public DateTime CommittedAt { get; set; }
 public TimeSpan? Duration { get; set; }
 public Dictionary<string, bool> ServiceResults { get; set; } = new();
}

public enum IsolationLevel
{
 ReadUncommitted,
 ReadCommitted,
 RepeatableRead,
 Serializable
}

public enum TransactionStatus
{
 Active,
 Preparing,
 Prepared,
 Committing,
 Committed,
 Aborting,
 Aborted
}

// Activity models

public class BeginTransactionRequest {
 public string TransactionId { get; set; } = string.Empty;
 public List<string> Services { get; set; } = new();
 public IsolationLevel IsolationLevel { get; set; }
 public TimeSpan Timeout { get; set; }
}

public class BeginTransactionResult {
 public string GlobalTransactionId { get; set; } = string.Empty;
 public string CoordinatorEndpoint { get; set; } = string.Empty;
 public DateTime ExpiresAt { get; set; }
}

public class PrepareServiceRequest {
 public string ServiceName { get; set; } = string.Empty;
 public string GlobalTransactionId { get; set; } = string.Empty;
 public Dictionary<string, object> Data { get; set; } = new();
}

public class PrepareServiceResult {
 public string ServiceName { get; set; } = string.Empty;
 public bool IsPrepared { get; set; }
 public string PrepareToken { get; set; } = string.Empty;
 public string? ErrorMessage { get; set; }
}

public class ValidatePreparationRequest {
 public string GlobalTransactionId { get; set; } = string.Empty;
 public List<PrepareServiceResult> ServiceResults { get; set; } = new();
}

public class ValidatePreparationResult {
 public bool AllPrepared { get; set; }
 public List<string> FailedServices { get; set; } = new();
}

public class CommitServiceRequest {
 public string ServiceName { get; set; } = string.Empty;
 public string GlobalTransactionId { get; set; } = string.Empty;
 public string? PrepareToken { get; set; }
 public string[] DependsOn { get; set; } = Array.Empty<string>();
}

public class CommitServiceResult {
 public string ServiceName { get; set; } = string.Empty;
 public bool IsSuccessful { get; set; }
 public string CommitToken { get; set; } = string.Empty;
 public DateTime CommittedAt { get; set; }
}

public class CompleteTransactionRequest {
 public string GlobalTransactionId { get; set; } = string.Empty;
 public TransactionStatus Status { get; set; }
 public List<CommitServiceResult> CommitResults { get; set; } = new();
}

public class CompleteTransactionResult {
 public bool IsCompleted { get; set; }
 public DateTime CompletedAt { get; set; }
 public string FinalStatus { get; set; } = string.Empty;
}

public class AuditRequest {
 public string TransactionId { get; set; } = string.Empty;
 public string GlobalTransactionId { get; set; } = string.Empty;
 public List<string> Services { get; set; } = new();
 public string Status { get; set; } = string.Empty;
 public TimeSpan? Duration { get; set; }
 public List<AuditStep> Steps { get; set; } = new();
}

public class AuditStep {
 public string StepName { get; set; } = string.Empty;
 public string Status { get; set; } = string.Empty;
 public DateTime StartTime { get; set; }
 public DateTime? EndTime { get; set; }
 public TimeSpan? Duration { get; set; }
}