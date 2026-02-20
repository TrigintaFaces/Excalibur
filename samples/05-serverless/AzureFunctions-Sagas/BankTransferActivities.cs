using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Excalibur.Hosting.AzureFunctions;

namespace examples.Excalibur.Dispatch.Examples.Serverless.Azure.Sagas;

/// <summary>
/// Activity functions for the bank transfer saga.
/// </summary>
public class BankTransferActivities {
 private readonly ILogger<BankTransferActivities> _logger;

 // Simulated in-memory account store (in real world, this would be a database)
 private static readonly Dictionary<string, Account> _accounts = new()
 {
 ["ACC-001"] = new Account { AccountId = "ACC-001", Balance = 10000, Currency = "USD", IsActive = true },
 ["ACC-002"] = new Account { AccountId = "ACC-002", Balance = 5000, Currency = "USD", IsActive = true },
 ["ACC-003"] = new Account { AccountId = "ACC-003", Balance = 15000, Currency = "USD", IsActive = true },
 ["ACC-004"] = new Account { AccountId = "ACC-004", Balance = 0, Currency = "USD", IsActive = true }
 };

 private static readonly Dictionary<string, AccountLock> _locks = new();
 private static readonly object _lockSync = new();

 /// <summary>
 /// Initializes a new instance of the <see cref="BankTransferActivities"/> class.
 /// </summary>
 public BankTransferActivities(ILogger<BankTransferActivities> logger)
 {
 _logger = logger;
 }

 /// <summary>
 /// Validates a transfer request.
 /// </summary>
 [Function("ValidateTransferActivity")]
 public async Task<TransferValidationResult> ValidateTransfer(
 [ActivityTrigger] BankTransferRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Validating transfer from {Source} to {Destination} for {Amount} {Currency}",
 request.SourceAccountId, request.DestinationAccountId, request.Amount, request.Currency);

 await Task.Delay(100); // Simulate validation

 var errors = new List<string>();

 // Check if accounts exist
 var sourceExists = _accounts.ContainsKey(request.SourceAccountId);
 var destExists = _accounts.ContainsKey(request.DestinationAccountId);

 if (!sourceExists)
 {
 errors.Add($"Source account {request.SourceAccountId} not found");
 }

 if (!destExists)
 {
 errors.Add($"Destination account {request.DestinationAccountId} not found");
 }

 // Check sufficient balance
 var sufficientBalance = false;
 if (sourceExists)
 {
 var sourceAccount = _accounts[request.SourceAccountId];
 sufficientBalance = sourceAccount.Balance >= request.Amount;

 if (!sufficientBalance)
 {
 errors.Add($"Insufficient balance. Available: {sourceAccount.Balance}, Required: {request.Amount}");
 }

 if (!sourceAccount.IsActive)
 {
 errors.Add("Source account is not active");
 }
 }

 return new TransferValidationResult
 {
 IsValid = errors.Count == 0,
 SourceAccountExists = sourceExists,
 DestinationAccountExists = destExists,
 SufficientBalance = sufficientBalance,
 ValidationErrors = errors
 };
 }

 /// <summary>
 /// Locks an account for a transaction.
 /// </summary>
 [Function("LockAccountActivity")]
 public async Task<AccountLockResult> LockAccount(
 [ActivityTrigger] AccountLockRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Locking account {AccountId} for {LockType} of {Amount}",
 request.AccountId, request.LockType, request.Amount);

 await Task.Delay(50); // Simulate lock acquisition

 lock (_lockSync)
 {
 // Check if account is already locked
 if (_locks.ContainsKey(request.AccountId))
 {
 return new AccountLockResult
 {
 IsSuccessful = false,
 LockToken = string.Empty,
 LockedUntil = DateTime.UtcNow
 };
 }

 // Create lock
 var lockToken = Guid.NewGuid().ToString();
 var lockedUntil = DateTime.UtcNow.AddMinutes(5);

 _locks[request.AccountId] = new AccountLock
 {
 AccountId = request.AccountId,
 LockToken = lockToken,
 TransactionId = request.TransactionId,
 LockedUntil = lockedUntil,
 Amount = request.Amount,
 LockType = request.LockType
 };

 return new AccountLockResult
 {
 IsSuccessful = true,
 LockToken = lockToken,
 LockedUntil = lockedUntil
 };
 }
 }

 /// <summary>
 /// Unlocks an account.
 /// </summary>
 [Function("UnlockAccountActivity")]
 public async Task UnlockAccount(
 [ActivityTrigger] CompensationInput input,
 FunctionContext context)
 {
 var request = input.OriginalInput as AccountLockRequest;
 var result = input.OriginalOutput as AccountLockResult;

 _logger.LogInformation("Unlocking account {AccountId}", request?.AccountId);

 await Task.Delay(50); // Simulate unlock (_lockSync)
 {
 if (request?.AccountId != null && _locks.ContainsKey(request.AccountId))
 {
 var accountLock = _locks[request.AccountId];
 if (accountLock.LockToken == result?.LockToken)
 {
 _locks.Remove(request.AccountId);
 _logger.LogInformation("Account {AccountId} unlocked successfully", request.AccountId);
 }
 }
 }
 }

 /// <summary>
 /// Debits an account.
 /// </summary>
 [Function("DebitAccountActivity")]
 public async Task<AccountTransactionResult> DebitAccount(
 [ActivityTrigger] AccountTransactionRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Debiting account {AccountId} for {Amount}",
 request.AccountId, request.Amount);

 await Task.Delay(200); // Simulate transaction

 lock (_lockSync)
 {
 // Verify lock
 if (!_locks.ContainsKey(request.AccountId) ||
 _locks[request.AccountId].LockToken != request.LockToken)
 {
 throw new InvalidOperationException("Account is not locked or lock token is invalid");
 }

 var account = _accounts[request.AccountId];
 var previousBalance = account.Balance;

 if (account.Balance < request.Amount)
 {
 throw new InvalidOperationException("Insufficient balance");
 }

 account.Balance -= request.Amount;

 return new AccountTransactionResult
 {
 TransactionReference = $"DR-{Guid.NewGuid():N}".Substring(0, 12).ToUpper(),
 PreviousBalance = previousBalance,
 NewBalance = account.Balance,
 ProcessedAt = DateTime.UtcNow,
 IsSuccessful = true
 };
 }
 }

 /// <summary>
 /// Reverses a debit transaction.
 /// </summary>
 [Function("ReverseDebitActivity")]
 public async Task ReverseDebit(
 [ActivityTrigger] CompensationInput input,
 FunctionContext context)
 {
 var request = input.OriginalInput as AccountTransactionRequest;
 var result = input.OriginalOutput as AccountTransactionResult;

 _logger.LogInformation("Reversing debit for account {AccountId}, amount {Amount}",
 request?.AccountId, request?.Amount);

 await Task.Delay(150); // Simulate reversal

 lock (_lockSync)
 {
 if (request?.AccountId != null && _accounts.ContainsKey(request.AccountId))
 {
 var account = _accounts[request.AccountId];
 account.Balance += request.Amount; // Add back the debited amount

 _logger.LogInformation("Debit reversed. New balance: {Balance}", account.Balance);
 }
 }
 }

 /// <summary>
 /// Credits an account.
 /// </summary>
 [Function("CreditAccountActivity")]
 public async Task<AccountTransactionResult> CreditAccount(
 [ActivityTrigger] AccountTransactionRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Crediting account {AccountId} for {Amount}",
 request.AccountId, request.Amount);

 await Task.Delay(200); // Simulate transaction

 lock (_lockSync)
 {
 // Verify lock
 if (!_locks.ContainsKey(request.AccountId) ||
 _locks[request.AccountId].LockToken != request.LockToken)
 {
 throw new InvalidOperationException("Account is not locked or lock token is invalid");
 }

 var account = _accounts[request.AccountId];
 var previousBalance = account.Balance;

 account.Balance += request.Amount;

 return new AccountTransactionResult
 {
 TransactionReference = $"CR-{Guid.NewGuid():N}".Substring(0, 12).ToUpper(),
 PreviousBalance = previousBalance,
 NewBalance = account.Balance,
 ProcessedAt = DateTime.UtcNow,
 IsSuccessful = true
 };
 }
 }

 /// <summary>
 /// Reverses a credit transaction.
 /// </summary>
 [Function("ReverseCreditActivity")]
 public async Task ReverseCredit(
 [ActivityTrigger] CompensationInput input,
 FunctionContext context)
 {
 var request = input.OriginalInput as AccountTransactionRequest;
 var result = input.OriginalOutput as AccountTransactionResult;

 _logger.LogInformation("Reversing credit for account {AccountId}, amount {Amount}",
 request?.AccountId, request?.Amount);

 await Task.Delay(150); // Simulate reversal

 lock (_lockSync)
 {
 if (request?.AccountId != null && _accounts.ContainsKey(request.AccountId))
 {
 var account = _accounts[request.AccountId];
 account.Balance -= request.Amount; // Remove the credited amount

 _logger.LogInformation("Credit reversed. New balance: {Balance}", account.Balance);
 }
 }
 }

 /// <summary>
 /// Records a transaction in the ledger.
 /// </summary>
 [Function("RecordLedgerEntryActivity")]
 public async Task<LedgerEntryResult> RecordLedgerEntry(
 [ActivityTrigger] LedgerEntryRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Recording ledger entry for transaction {TransactionId}",
 request.TransactionId);

 await Task.Delay(100); // Simulate ledger write

 // In real world, this would write to a ledger database
 var reference = $"LEDGER-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 20).ToUpper();

 _logger.LogInformation("Ledger entry recorded with reference {Reference}", reference);

 return new LedgerEntryResult
 {
 Reference = reference,
 IsRecorded = true
 };
 }

 /// <summary>
 /// Unlocks multiple accounts.
 /// </summary>
 [Function("UnlockAccountsActivity")]
 public async Task<bool> UnlockAccounts(
 [ActivityTrigger] UnlockAccountsRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Unlocking {Count} accounts", request.AccountIds.Length);

 await Task.Delay(100); // Simulate unlock (_lockSync)
 {
 foreach (var accountId in request.AccountIds)
 {
 if (_locks.ContainsKey(accountId))
 {
 var accountLock = _locks[accountId];
 if (accountLock.TransactionId == request.TransactionId)
 {
 _locks.Remove(accountId);
 _logger.LogInformation("Account {AccountId} unlocked", accountId);
 }
 }
 }
 }

 return true;
 }

 /// <summary>
 /// Sends transfer notifications.
 /// </summary>
 [Function("SendTransferNotificationsActivity")]
 public async Task<bool> SendTransferNotifications(
 [ActivityTrigger] TransferNotificationRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Sending notifications for transfer {TransactionId}",
 request.TransactionId);

 await Task.Delay(200); // Simulate notification sending

 // In real world, this would send emails/SMS/push notifications
 _logger.LogInformation("Notification sent to source account {Source}", request.SourceAccountId);
 _logger.LogInformation("Notification sent to destination account {Destination}", request.DestinationAccountId);

 return true;
 }

 // Helper classes
 private class Account {
 public string AccountId { get; set; } = string.Empty;
 public decimal Balance { get; set; }
 public string Currency { get; set; } = string.Empty;
 public bool IsActive { get; set; }
 }

 private class AccountLock {
 public string AccountId { get; set; } = string.Empty;
 public string LockToken { get; set; } = string.Empty;
 public string TransactionId { get; set; } = string.Empty;
 public DateTime LockedUntil { get; set; }
 public decimal Amount { get; set; }
 public AccountLockType LockType { get; set; }
 }
}