using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using IntegrationMiddleware.Functions.Models;
using IntegrationMiddleware.Functions.Services;

namespace IntegrationMiddleware.Functions.Functions;

/*
 * ============================================================================
 * AccountEventProcessor.cs - Service Bus Triggered Azure Function
 * ============================================================================
 * 
 * DESIGN DECISIONS:
 * -----------------
 * This function processes account events from Azure Service Bus. I chose
 * Service Bus over Event Grid because:
 * 
 * 1. GUARANTEED DELIVERY: Service Bus provides at-least-once delivery with DLQ
 * 2. ORDERING: Session-enabled queues can maintain FIFO per account
 * 3. TRANSACTIONS: Can participate in distributed transactions if needed
 * 4. MATURE: Well-understood patterns for enterprise integration
 * 
 * MESSAGE FLOW:
 * -------------
 * Dataverse → Plugin/Flow → Service Bus → This Function → External API
 * 
 * The message originates from a Dataverse plugin or Power Automate flow
 * that fires on Account create/update, serializes the event, and publishes
 * to Service Bus.
 * 
 * RETRY STRATEGY:
 * ---------------
 * Azure Functions + Service Bus provides automatic retry:
 * 1. Function throws exception → Message returns to queue
 * 2. After maxDeliveryCount attempts → Message moves to Dead Letter Queue (DLQ)
 * 3. Default maxDeliveryCount is 10 (configured on queue/subscription)
 * 
 * I rely on this built-in retry rather than implementing custom retry
 * because it's:
 * - Simpler to maintain
 * - Handles function crashes/restarts
 * - Provides automatic DLQ for poison messages
 * 
 * IDEMPOTENCY:
 * ------------
 * For banking scenarios, idempotency is critical. Strategies I would use:
 * 1. Store processed CorrelationIds in Redis/Cosmos (with TTL)
 * 2. Use conditional updates in target systems (If-Match, upsert)
 * 3. Design operations to be naturally idempotent (PUT over POST)
 * 
 * NOT IMPLEMENTED (MVP):
 * - Idempotency check (would use Redis in production)
 * - Session handling for ordered processing
 * - Batching for high throughput
 * - DLQ monitoring/alerting
 * 
 * BANKING/SECURITY:
 * -----------------
 * - All operations logged with CorrelationId for audit trail
 * - PII fields would be encrypted in transit and at rest
 * - Managed Identity for all Azure service authentication
 */

public class AccountEventProcessor
{
    private readonly IExternalApiService _externalApiService;
    private readonly IDataverseService _dataverseService;
    private readonly ILogger<AccountEventProcessor> _logger;

    public AccountEventProcessor(
        IExternalApiService externalApiService,
        IDataverseService dataverseService,
        ILogger<AccountEventProcessor> logger)
    {
        _externalApiService = externalApiService;
        _dataverseService = dataverseService;
        _logger = logger;
    }

    /// <summary>
    /// Processes account events from Service Bus queue.
    /// Triggered automatically when messages arrive on the configured queue.
    /// </summary>
    /// <param name="message">Service Bus message containing AccountEvent</param>
    /// <remarks>
    /// PRODUCTION CONSIDERATIONS:
    /// - Add idempotency check before processing
    /// - Add structured logging for observability
    /// - Add metrics for monitoring (processing time, success rate)
    /// </remarks>
    [Function(nameof(ProcessAccountEvent))]
    public async Task ProcessAccountEvent(
        [ServiceBusTrigger(
            "account-events",
            Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
        FunctionContext context)
    {
        // Extract correlation ID - critical for distributed tracing
        var correlationId = message.CorrelationId ?? Guid.NewGuid().ToString();

        _logger.LogInformation(
            "[{CorrelationId}] Processing account event. MessageId: {MessageId}, DeliveryCount: {DeliveryCount}",
            correlationId, message.MessageId, message.DeliveryCount);

        try
        {
            // Deserialize the message body
            var accountEvent = JsonSerializer.Deserialize<AccountEvent>(
                message.Body.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (accountEvent == null)
            {
                _logger.LogError(
                    "[{CorrelationId}] Failed to deserialize message. Body: {Body}",
                    correlationId, message.Body.ToString());
                
                // Don't throw - message will go to DLQ after max retries
                // In production, might want to throw to trigger retry
                throw new InvalidOperationException("Failed to deserialize AccountEvent");
            }

            // Use correlation ID from message if present
            if (!string.IsNullOrEmpty(accountEvent.CorrelationId))
            {
                correlationId = accountEvent.CorrelationId;
            }

            _logger.LogInformation(
                "[{CorrelationId}] Account event received. EventType: {EventType}, EntityId: {EntityId}",
                correlationId, accountEvent.EventType, accountEvent.EntityId);

            // =================================================================
            // IDEMPOTENCY CHECK (NOT IMPLEMENTED - MVP)
            // =================================================================
            // In production, I would check if this message was already processed:
            //
            // var alreadyProcessed = await _idempotencyService.CheckAsync(
            //     correlationId, accountEvent.EntityId);
            // if (alreadyProcessed)
            // {
            //     _logger.LogWarning(
            //         "[{CorrelationId}] Duplicate message detected. Skipping.",
            //         correlationId);
            //     return;
            // }
            // =================================================================

            // Validate the event has required data
            if (accountEvent.Data == null)
            {
                _logger.LogWarning(
                    "[{CorrelationId}] Event data is null. Fetching from Dataverse.",
                    correlationId);

                // Fallback: fetch current data from Dataverse
                accountEvent.Data = await _dataverseService.GetAccountAsync(
                    accountEvent.EntityId, correlationId);

                if (accountEvent.Data == null)
                {
                    throw new InvalidOperationException(
                        $"Account {accountEvent.EntityId} not found in Dataverse");
                }
            }

            // =================================================================
            // VALIDATION (BANKING SCENARIO)
            // =================================================================
            // For banking, we might validate the account before syncing
            var isValid = await _externalApiService.ValidateAccountAsync(
                accountEvent.Data, correlationId);

            if (!isValid)
            {
                _logger.LogWarning(
                    "[{CorrelationId}] Account {AccountId} failed validation. Will retry.",
                    correlationId, accountEvent.EntityId);
                
                // Throw to trigger Service Bus retry
                throw new InvalidOperationException(
                    $"Account validation failed for {accountEvent.EntityId}");
            }

            // =================================================================
            // SYNC TO EXTERNAL SYSTEM
            // =================================================================
            var syncSuccess = await _externalApiService.SyncAccountAsync(
                accountEvent.Data, accountEvent.EventType, correlationId);

            if (!syncSuccess)
            {
                _logger.LogError(
                    "[{CorrelationId}] Failed to sync account {AccountId} to external system",
                    correlationId, accountEvent.EntityId);
                
                // Throw to trigger Service Bus retry
                throw new InvalidOperationException(
                    $"External sync failed for {accountEvent.EntityId}");
            }

            // =================================================================
            // MARK AS PROCESSED (NOT IMPLEMENTED - MVP)
            // =================================================================
            // In production, record successful processing for idempotency:
            //
            // await _idempotencyService.MarkProcessedAsync(
            //     correlationId, accountEvent.EntityId);
            // =================================================================

            _logger.LogInformation(
                "[{CorrelationId}] Account event processed successfully. EventType: {EventType}, EntityId: {EntityId}",
                correlationId, accountEvent.EventType, accountEvent.EntityId);
        }
        catch (JsonException ex)
        {
            // JSON parsing errors - likely a malformed message
            _logger.LogError(ex,
                "[{CorrelationId}] JSON deserialization failed. Message will be dead-lettered after max retries.",
                correlationId);
            throw; // Rethrow to trigger retry/DLQ
        }
        catch (Exception ex)
        {
            // Log and rethrow to trigger Service Bus retry
            _logger.LogError(ex,
                "[{CorrelationId}] Error processing account event. DeliveryCount: {DeliveryCount}",
                correlationId, message.DeliveryCount);
            throw;
        }
    }
}

/*
 * ============================================================================
 * DEAD LETTER QUEUE (DLQ) HANDLING
 * ============================================================================
 * 
 * NOT IMPLEMENTED (MVP) - I would add a separate function to process DLQ:
 * 
 * [Function(nameof(ProcessDeadLetteredAccountEvent))]
 * public async Task ProcessDeadLetteredAccountEvent(
 *     [ServiceBusTrigger(
 *         "account-events/$deadletterqueue",
 *         Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message)
 * {
 *     // 1. Log the failure for investigation
 *     // 2. Send alert to operations team
 *     // 3. Store in error tracking system (Cosmos, SQL)
 *     // 4. Optionally attempt repair and republish
 * }
 * 
 * ============================================================================
 */
