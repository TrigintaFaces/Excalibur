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
/// Example of a bank transfer saga demonstrating distributed transaction with consistency.
/// </summary>
public class BankTransferSaga : SagaOrchestrationBase<BankTransferRequest, BankTransferResult>
{
 /// <summary>
 /// Initializes a new instance of the <see cref="BankTransferSaga"/> class.
 /// </summary>
 public BankTransferSaga(ILogger<BankTransferSaga> logger) : base(logger)
 {
 }

 /// <inheritdoc />
 protected override string SagaName => "BankTransfer";

 /// <inheritdoc />
 protected override TimeSpan SagaTimeout => TimeSpan.FromMinutes(5);

 /// <inheritdoc />
 protected override void ConfigureSteps()
 {
 // Step 1: Validate transfer request
 AddStep<BankTransferRequest, TransferValidationResult>(
 "ValidateTransfer",
 "ValidateTransferActivity",
 (input, state) => input,
 (output, state) => state.CustomData["ValidationResult"] = output);

 // Step 2: Lock source account
 AddStep<AccountLockRequest, AccountLockResult>(
 "LockSourceAccount",
 "LockAccountActivity",
 (input, state) => new AccountLockRequest
 {
 AccountId = input.SourceAccountId,
 Amount = input.Amount,
 LockType = AccountLockType.Debit,
 TransactionId = state.SagaId
 },
 (output, state) => state.CustomData["SourceLock"] = output,
 "UnlockAccountActivity");

 // Step 3: Lock destination account
 AddStep<AccountLockRequest, AccountLockResult>(
 "LockDestinationAccount",
 "LockAccountActivity",
 (input, state) => new AccountLockRequest
 {
 AccountId = input.DestinationAccountId,
 Amount = input.Amount,
 LockType = AccountLockType.Credit,
 TransactionId = state.SagaId
 },
 (output, state) => state.CustomData["DestinationLock"] = output,
 "UnlockAccountActivity");

 // Step 4: Debit source account
 AddStep<AccountTransactionRequest, AccountTransactionResult>(
 "DebitSourceAccount",
 "DebitAccountActivity",
 (input, state) => new AccountTransactionRequest
 {
 AccountId = input.SourceAccountId,
 Amount = input.Amount,
 TransactionId = state.SagaId,
 Description = $"Transfer to {input.DestinationAccountId}",
 LockToken = (state.CustomData["SourceLock"] as AccountLockResult)?.LockToken
 },
 (output, state) => state.CustomData["DebitResult"] = output,
 "ReverseDebitActivity");

 // Step 5: Credit destination account
 AddStep<AccountTransactionRequest, AccountTransactionResult>(
 "CreditDestinationAccount",
 "CreditAccountActivity",
 (input, state) => new AccountTransactionRequest
 {
 AccountId = input.DestinationAccountId,
 Amount = input.Amount,
 TransactionId = state.SagaId,
 Description = $"Transfer from {input.SourceAccountId}",
 LockToken = (state.CustomData["DestinationLock"] as AccountLockResult)?.LockToken
 },
 (output, state) => state.CustomData["CreditResult"] = output,
 "ReverseCreditActivity");

 // Step 6: Record transaction in ledger
 AddStep<LedgerEntryRequest, LedgerEntryResult>(
 "RecordInLedger",
 "RecordLedgerEntryActivity",
 (input, state) => new LedgerEntryRequest
 {
 TransactionId = state.SagaId,
 SourceAccountId = input.SourceAccountId,
 DestinationAccountId = input.DestinationAccountId,
 Amount = input.Amount,
 Currency = input.Currency,
 Description = input.Description,
 Timestamp = state.StartTime
 },
 (output, state) => state.CustomData["LedgerEntry"] = output);

 // Step 7: Unlock accounts (always execute)
 AddStep<UnlockAccountsRequest, bool>(
 "UnlockAccounts",
 "UnlockAccountsActivity",
 (input, state) => new UnlockAccountsRequest
 {
 AccountIds = new[] { input.SourceAccountId, input.DestinationAccountId },
 TransactionId = state.SagaId
 });

 // Step 8: Send notifications
 AddStep<TransferNotificationRequest, bool>(
 "SendNotifications",
 "SendTransferNotificationsActivity",
 (input, state) => new TransferNotificationRequest
 {
 SourceAccountId = input.SourceAccountId,
 DestinationAccountId = input.DestinationAccountId,
 Amount = input.Amount,
 Currency = input.Currency,
 TransactionId = state.SagaId,
 Status = "Completed"
 });
 }

 /// <inheritdoc />
 protected override async Task ValidateInputAsync(
 TaskOrchestrationContext context,
 BankTransferRequest input,
 SagaState sagaState)
 {
 if (input.Amount <= 0)
 {
 throw new ArgumentException("Transfer amount must be positive");
 }

 if (input.SourceAccountId == input.DestinationAccountId)
 {
 throw new ArgumentException("Cannot transfer to the same account");
 }

 if (string.IsNullOrEmpty(input.Currency))
 {
 throw new ArgumentException("Currency is required");
 }

 await Task.CompletedTask;
 }

 /// <inheritdoc />
 protected override Task<BankTransferResult> CreateOutputAsync(
 TaskOrchestrationContext context,
 BankTransferRequest input,
 SagaState sagaState)
 {
 var debitResult = sagaState.CustomData["DebitResult"] as AccountTransactionResult;
 var creditResult = sagaState.CustomData["CreditResult"] as AccountTransactionResult;
 var ledgerEntry = sagaState.CustomData["LedgerEntry"] as LedgerEntryResult;

 return Task.FromResult(new BankTransferResult
 {
 TransactionId = sagaState.SagaId,
 Status = "Completed",
 SourceAccountId = input.SourceAccountId,
 DestinationAccountId = input.DestinationAccountId,
 Amount = input.Amount,
 Currency = input.Currency,
 SourceNewBalance = debitResult?.NewBalance ?? 0,
 DestinationNewBalance = creditResult?.NewBalance ?? 0,
 LedgerReference = ledgerEntry?.Reference,
 CompletedAt = sagaState.EndTime ?? DateTime.UtcNow,
 ProcessingTime = sagaState.Duration
 });
 }
}

// Models for Bank Transfer

public class BankTransferRequest {
 public string SourceAccountId { get; set; } = string.Empty;
 public string DestinationAccountId { get; set; } = string.Empty;
 public decimal Amount { get; set; }
 public string Currency { get; set; } = "USD";
 public string Description { get; set; } = string.Empty;
 public TransferType TransferType { get; set; } = TransferType.Standard;
 public Dictionary<string, string> Metadata { get; set; } = new();
}

public class BankTransferResult {
 public string TransactionId { get; set; } = string.Empty;
 public string Status { get; set; } = string.Empty;
 public string SourceAccountId { get; set; } = string.Empty;
 public string DestinationAccountId { get; set; } = string.Empty;
 public decimal Amount { get; set; }
 public string Currency { get; set; } = string.Empty;
 public decimal SourceNewBalance { get; set; }
 public decimal DestinationNewBalance { get; set; }
 public string? LedgerReference { get; set; }
 public DateTime CompletedAt { get; set; }
 public TimeSpan? ProcessingTime { get; set; }
}

public enum TransferType
{
 Standard,
 Express,
 Wire
}

// Activity models

public class TransferValidationResult {
 public bool IsValid { get; set; }
 public bool SourceAccountExists { get; set; }
 public bool DestinationAccountExists { get; set; }
 public bool SufficientBalance { get; set; }
 public List<string> ValidationErrors { get; set; } = new();
}

public class AccountLockRequest {
 public string AccountId { get; set; } = string.Empty;
 public decimal Amount { get; set; }
 public AccountLockType LockType { get; set; }
 public string TransactionId { get; set; } = string.Empty;
}

public class AccountLockResult {
 public string LockToken { get; set; } = string.Empty;
 public DateTime LockedUntil { get; set; }
 public bool IsSuccessful { get; set; }
}

public enum AccountLockType
{
 Debit,
 Credit
}

public class AccountTransactionRequest {
 public string AccountId { get; set; } = string.Empty;
 public decimal Amount { get; set; }
 public string TransactionId { get; set; } = string.Empty;
 public string Description { get; set; } = string.Empty;
 public string? LockToken { get; set; }
}

public class AccountTransactionResult {
 public string TransactionReference { get; set; } = string.Empty;
 public decimal PreviousBalance { get; set; }
 public decimal NewBalance { get; set; }
 public DateTime ProcessedAt { get; set; }
 public bool IsSuccessful { get; set; }
}

public class LedgerEntryRequest {
 public string TransactionId { get; set; } = string.Empty;
 public string SourceAccountId { get; set; } = string.Empty;
 public string DestinationAccountId { get; set; } = string.Empty;
 public decimal Amount { get; set; }
 public string Currency { get; set; } = string.Empty;
 public string Description { get; set; } = string.Empty;
 public DateTime Timestamp { get; set; }
}

public class LedgerEntryResult {
 public string Reference { get; set; } = string.Empty;
 public bool IsRecorded { get; set; }
}

public class UnlockAccountsRequest {
 public string[] AccountIds { get; set; } = Array.Empty<string>();
 public string TransactionId { get; set; } = string.Empty;
}

public class TransferNotificationRequest {
 public string SourceAccountId { get; set; } = string.Empty;
 public string DestinationAccountId { get; set; } = string.Empty;
 public decimal Amount { get; set; }
 public string Currency { get; set; } = string.Empty;
 public string TransactionId { get; set; } = string.Empty;
 public string Status { get; set; } = string.Empty;
}