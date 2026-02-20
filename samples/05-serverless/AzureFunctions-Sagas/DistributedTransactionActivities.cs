using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Excalibur.Hosting.AzureFunctions;

namespace examples.Excalibur.Dispatch.Examples.Serverless.Azure.Sagas;

/// <summary>
/// Activity functions for the distributed transaction saga.
/// </summary>
public class DistributedTransactionActivities {
 private readonly ILogger<DistributedTransactionActivities> _logger;

 // Simulated distributed transaction state
 private static readonly Dictionary<string, TransactionState> _transactions = new();
 private static readonly Dictionary<string, ServicePrepareState> _servicePrepareStates = new();
 private static readonly Dictionary<string, ServiceCommitState> _serviceCommitStates = new();
 private static readonly object _stateLock = new();

 /// <summary>
 /// Initializes a new instance of the <see cref="DistributedTransactionActivities"/> class.
 /// </summary>
 public DistributedTransactionActivities(ILogger<DistributedTransactionActivities> logger)
 {
 _logger = logger;
 }

 /// <summary>
 /// Begins a distributed transaction across multiple services.
 /// </summary>
 [Function("BeginDistributedTransactionActivity")]
 public async Task<BeginTransactionResult> BeginDistributedTransaction(
 [ActivityTrigger] BeginTransactionRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Beginning distributed transaction {TransactionId} across {ServiceCount} services",
 request.TransactionId, request.Services.Count);

 await Task.Delay(100); // Simulate coordinator setup

 var globalTransactionId = $"DTX-{Guid.NewGuid():N}".Substring(0, 16).ToUpper();

 lock (_stateLock)
 {
 _transactions[globalTransactionId] = new TransactionState
 {
 GlobalTransactionId = globalTransactionId,
 LocalTransactionId = request.TransactionId,
 Services = request.Services,
 Status = TransactionStatus.Active,
 StartedAt = DateTime.UtcNow,
 ExpiresAt = DateTime.UtcNow.Add(request.Timeout)
 };
 }

 return new BeginTransactionResult
 {
 GlobalTransactionId = globalTransactionId,
 CoordinatorEndpoint = "tcp://coordinator.example.com:5000",
 ExpiresAt = DateTime.UtcNow.Add(request.Timeout)
 };
 }

 /// <summary>
 /// Aborts a distributed transaction.
 /// </summary>
 [Function("AbortDistributedTransactionActivity")]
 public async Task AbortDistributedTransaction(
 [ActivityTrigger] CompensationInput input,
 FunctionContext context)
 {
 var result = input.OriginalOutput as BeginTransactionResult;

 _logger.LogInformation("Aborting distributed transaction {GlobalTransactionId}",
 result?.GlobalTransactionId);

 await Task.Delay(100); // Simulate abort

 lock (_stateLock)
 {
 if (result?.GlobalTransactionId != null && _transactions.TryGetValue(result.GlobalTransactionId, out var transaction))
 {
 transaction.Status = TransactionStatus.Aborted;
 transaction.CompletedAt = DateTime.UtcNow;
 }
 }
 }

 /// <summary>
 /// Prepares a service for the distributed transaction (2PC prepare phase).
 /// </summary>
 [Function("PrepareServiceActivity")]
 public async Task<PrepareServiceResult> PrepareService(
 [ActivityTrigger] PrepareServiceRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Preparing service {ServiceName} for transaction {GlobalTransactionId}",
 request.ServiceName, request.GlobalTransactionId);

 // Simulate different service preparation times
 var delay = request.ServiceName switch
 {
 "ServiceA" => 200,
 "ServiceB" => 300,
 "ServiceC" => 250,
 _ => 150
 };

 await Task.Delay(delay);

 // Simulate occasional failures
 var random = new Random();
 var shouldFail = random.Next(100) < 5; // 5% failure rate

 if (shouldFail)
 {
 return new PrepareServiceResult
 {
 ServiceName = request.ServiceName,
 IsPrepared = false,
 PrepareToken = string.Empty,
 ErrorMessage = $"Service {request.ServiceName} failed to prepare: Simulated failure"
 };
 }

 var prepareToken = $"PREP-{request.ServiceName}-{Guid.NewGuid():N}".Substring(0, 20).ToUpper();

 lock (_stateLock)
 {
 var key = $"{request.GlobalTransactionId}:{request.ServiceName}";
 _servicePrepareStates[key] = new ServicePrepareState
 {
 ServiceName = request.ServiceName,
 GlobalTransactionId = request.GlobalTransactionId,
 PrepareToken = prepareToken,
 PreparedAt = DateTime.UtcNow,
 Data = request.Data
 };
 }

 return new PrepareServiceResult
 {
 ServiceName = request.ServiceName,
 IsPrepared = true,
 PrepareToken = prepareToken
 };
 }

 /// <summary>
 /// Rolls back a prepared service.
 /// </summary>
 [Function("RollbackServiceActivity")]
 public async Task RollbackService(
 [ActivityTrigger] CompensationInput input,
 FunctionContext context)
 {
 var request = input.OriginalInput as PrepareServiceRequest;
 var result = input.OriginalOutput as PrepareServiceResult;

 _logger.LogInformation("Rolling back service {ServiceName} for transaction {GlobalTransactionId}",
 request?.ServiceName, request?.GlobalTransactionId);

 await Task.Delay(150); // Simulate rollback

 lock (_stateLock)
 {
 if (request != null)
 {
 var key = $"{request.GlobalTransactionId}:{request.ServiceName}";
 _servicePrepareStates.Remove(key);
 }
 }

 _logger.LogInformation("Service {ServiceName} rolled back successfully", request?.ServiceName);
 }

 /// <summary>
 /// Validates that all services are prepared successfully.
 /// </summary>
 [Function("ValidatePreparationActivity")]
 public async Task<ValidatePreparationResult> ValidatePreparation(
 [ActivityTrigger] ValidatePreparationRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Validating preparation for transaction {GlobalTransactionId}",
 request.GlobalTransactionId);

 await Task.Delay(50); // Simulate validation

 var failedServices = request.ServiceResults
 .Where(r => !r.IsPrepared)
 .Select(r => r.ServiceName)
 .ToList();

 var allPrepared = !failedServices.Any();

 if (!allPrepared)
 {
 _logger.LogWarning("Not all services prepared. Failed: {FailedServices}",
 string.Join(", ", failedServices));
 }

 return new ValidatePreparationResult
 {
 AllPrepared = allPrepared,
 FailedServices = failedServices
 };
 }

 /// <summary>
 /// Commits a service in the distributed transaction.
 /// </summary>
 [Function("CommitServiceActivity")]
 public async Task<CommitServiceResult> CommitService(
 [ActivityTrigger] CommitServiceRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Committing service {ServiceName} for transaction {GlobalTransactionId}",
 request.ServiceName, request.GlobalTransactionId);

 // Check dependencies
 if (request.DependsOn.Any())
 {
 _logger.LogInformation("Service {ServiceName} depends on: {Dependencies}",
 request.ServiceName, string.Join(", ", request.DependsOn));

 // Simulate checking dependencies
 await Task.Delay(100);
 }

 // Simulate commit
 await Task.Delay(300);

 var commitToken = $"COMMIT-{request.ServiceName}-{Guid.NewGuid():N}".Substring(0, 22).ToUpper();

 lock (_stateLock)
 {
 var key = $"{request.GlobalTransactionId}:{request.ServiceName}";
 _serviceCommitStates[key] = new ServiceCommitState
 {
 ServiceName = request.ServiceName,
 GlobalTransactionId = request.GlobalTransactionId,
 CommitToken = commitToken,
 CommittedAt = DateTime.UtcNow,
 PrepareToken = request.PrepareToken
 };
 }

 return new CommitServiceResult
 {
 ServiceName = request.ServiceName,
 IsSuccessful = true,
 CommitToken = commitToken,
 CommittedAt = DateTime.UtcNow
 };
 }

 /// <summary>
 /// Compensates a committed service.
 /// </summary>
 [Function("CompensateServiceActivity")]
 public async Task CompensateService(
 [ActivityTrigger] CompensationInput input,
 FunctionContext context)
 {
 var request = input.OriginalInput as CommitServiceRequest;
 var result = input.OriginalOutput as CommitServiceResult;

 _logger.LogInformation("Compensating service {ServiceName} for transaction {GlobalTransactionId}",
 request?.ServiceName, request?.GlobalTransactionId);

 await Task.Delay(200); // Simulate compensation

 lock (_stateLock)
 {
 if (request != null)
 {
 var key = $"{request.GlobalTransactionId}:{request.ServiceName}";
 _serviceCommitStates.Remove(key);
 }
 }

 _logger.LogInformation("Service {ServiceName} compensated successfully", request?.ServiceName);
 }

 /// <summary>
 /// Completes the distributed transaction.
 /// </summary>
 [Function("CompleteDistributedTransactionActivity")]
 public async Task<CompleteTransactionResult> CompleteDistributedTransaction(
 [ActivityTrigger] CompleteTransactionRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Completing distributed transaction {GlobalTransactionId} with status {Status}",
 request.GlobalTransactionId, request.Status);

 await Task.Delay(200); // Simulate completion

 lock (_stateLock)
 {
 if (_transactions.ContainsKey(request.GlobalTransactionId))
 {
 var transaction = _transactions[request.GlobalTransactionId];
 transaction.Status = request.Status;
 transaction.CompletedAt = DateTime.UtcNow;
 }
 }

 return new CompleteTransactionResult
 {
 IsCompleted = true,
 CompletedAt = DateTime.UtcNow,
 FinalStatus = request.Status.ToString()
 };
 }

 /// <summary>
 /// Records an audit trail for the transaction.
 /// </summary>
 [Function("RecordTransactionAuditActivity")]
 public async Task<bool> RecordTransactionAudit(
 [ActivityTrigger] AuditRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Recording audit trail for transaction {TransactionId}",
 request.TransactionId);

 await Task.Delay(100); // Simulate audit recording

 // Log audit summary
 _logger.LogInformation("Transaction audit: ID={TransactionId}, Status={Status}, Services={Services}, Duration={Duration}",
 request.TransactionId,
 request.Status,
 string.Join(", ", request.Services),
 request.Duration);

 // Log step details
 foreach (var step in request.Steps)
 {
 _logger.LogInformation(" Step: {StepName}, Status={Status}, Duration={Duration}",
 step.StepName,
 step.Status,
 step.Duration);
 }

 return true;
 }

 // Helper classes
 private class TransactionState {
 public string GlobalTransactionId { get; set; } = string.Empty;
 public string LocalTransactionId { get; set; } = string.Empty;
 public List<string> Services { get; set; } = new();
 public TransactionStatus Status { get; set; }
 public DateTime StartedAt { get; set; }
 public DateTime ExpiresAt { get; set; }
 public DateTime? CompletedAt { get; set; }
 }

 private class ServicePrepareState {
 public string ServiceName { get; set; } = string.Empty;
 public string GlobalTransactionId { get; set; } = string.Empty;
 public string PrepareToken { get; set; } = string.Empty;
 public DateTime PreparedAt { get; set; }
 public Dictionary<string, object> Data { get; set; } = new();
 }

 private class ServiceCommitState {
 public string ServiceName { get; set; } = string.Empty;
 public string GlobalTransactionId { get; set; } = string.Empty;
 public string CommitToken { get; set; } = string.Empty;
 public string? PrepareToken { get; set; }
 public DateTime CommittedAt { get; set; }
 }
}