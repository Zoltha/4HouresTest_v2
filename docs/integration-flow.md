# Integration Flow Documentation

## Overview

This document describes the integration flows implemented in the middleware, focusing on Account entity synchronization between Dynamics 365 CE and external systems.

## Flow 1: Account Create

### Trigger

An Account record is created in Dynamics 365 CE.

### Sequence

```
1. User creates Account in D365
       ↓
2. Dataverse Plugin/Flow fires (PostOperation)
       ↓
3. Plugin serializes AccountEvent and publishes to Service Bus
       ↓
4. Service Bus delivers message to queue
       ↓
5. Azure Function triggered (AccountEventProcessor)
       ↓
6. Function validates account (optional KYC check)
       ↓
7. Function syncs account to external system (POST)
       ↓
8. Message completed (removed from queue)
```

### Message Contract

```json
{
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "eventType": "Create",
  "timestamp": "2024-01-15T10:30:00Z",
  "entityName": "account",
  "entityId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "data": {
    "accountId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "accountNumber": "ACC-001",
    "name": "Contoso Ltd",
    "status": 0,
    "email": "info@contoso.com",
    "phone": "+1-555-0100"
  }
}
```

### Success Path

1. Message processed successfully
2. External system returns 201 Created
3. Message automatically completed
4. Log entry with CorrelationId

### Failure Path

1. External system returns error (5xx)
2. Function throws exception
3. Message returns to queue for retry
4. After max retries (default: 10), message moves to DLQ
5. Alert triggered for operations team

## Flow 2: Account Update

### Trigger

An Account record is updated in Dynamics 365 CE.

### Sequence

```
1. User updates Account in D365
       ↓
2. Dataverse Plugin/Flow fires (PostOperation)
       ↓
3. Plugin serializes AccountEvent and publishes to Service Bus
       ↓
4. Service Bus delivers message to queue
       ↓
5. Azure Function triggered (AccountEventProcessor)
       ↓
6. Function validates account (optional KYC check)
       ↓
7. Function syncs account to external system (PUT)
       ↓
8. Message completed (removed from queue)
```

### Message Contract

Same as Create, with `eventType: "Update"`.

### Idempotency Considerations

For banking scenarios, I designed for **at-least-once delivery**:

1. Messages may be processed more than once (network issues, restarts)
2. External system should use conditional updates (If-Match)
3. Production would track processed CorrelationIds in Redis

## Flow 3: HTTP CRUD API

### Create Account (POST /api/accounts)

```
1. Consumer sends POST request with account data
       ↓
2. Azure Function (HttpTrigger) receives request
       ↓
3. Function validates request body
       ↓
4. Function creates account in Dataverse
       ↓
5. Function returns 201 Created with account data
```

### Update Account (PUT /api/accounts/{id})

```
1. Consumer sends PUT request with account data
       ↓
2. Azure Function (HttpTrigger) receives request
       ↓
3. Function validates request body
       ↓
4. Function checks account exists in Dataverse
       ↓
5. Function updates account in Dataverse
       ↓
6. Function returns 200 OK with updated account
```

## Error Handling Strategy

### Transient Errors

| Error | Strategy |
|-------|----------|
| HTTP 500, 502, 503, 504 | Retry with exponential backoff |
| Timeout | Retry with exponential backoff |
| Connection refused | Retry with exponential backoff |

### Permanent Errors

| Error | Strategy |
|-------|----------|
| HTTP 400 Bad Request | Log and move to DLQ |
| HTTP 401 Unauthorized | Alert operations, investigate |
| HTTP 404 Not Found | Log warning, complete message |
| Deserialization failure | Log and move to DLQ |

### Dead Letter Queue Processing

```
1. Message exceeds max delivery count
       ↓
2. Service Bus moves to DLQ
       ↓
3. DLQ processor (not implemented in MVP) would:
   - Log failure details
   - Send alert to operations
   - Store in error tracking database
   - Optionally attempt repair and republish
```

## Correlation and Tracing

### CorrelationId Flow

```
Dynamics 365 → Plugin generates CorrelationId
                     ↓
              Service Bus message.CorrelationId
                     ↓
              Function logs with CorrelationId
                     ↓
              External API X-Correlation-Id header
                     ↓
              Application Insights operation_Id
```

### Log Structure

All logs include:
- CorrelationId
- Timestamp (UTC)
- Operation name
- Key business identifiers (AccountId)

Example:
```
[550e8400-e29b-41d4-a716-446655440000] Account event received. 
EventType: Create, EntityId: a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

## Performance Considerations

### Throughput

- Service Bus queue can handle 1000s of messages/second
- Azure Functions scale automatically based on queue depth
- External API is typically the bottleneck

### Latency

| Component | Expected Latency |
|-----------|------------------|
| D365 to Service Bus | 100-500ms |
| Service Bus to Function | 50-200ms |
| Function to External API | 200-2000ms |
| **Total End-to-End** | **500ms - 3s** |

## Monitoring and Alerting

### Key Metrics (Production)

| Metric | Alert Threshold |
|--------|-----------------|
| DLQ message count | > 0 |
| Processing failures/hour | > 10 |
| Average processing time | > 5s |
| Queue depth | > 1000 |

### Application Insights Queries

```kusto
// Failed message processing
traces
| where message contains "Error processing account event"
| project timestamp, customDimensions.CorrelationId, message

// Processing duration
requests
| where name == "ProcessAccountEvent"
| summarize avg(duration), max(duration), percentile(duration, 95) by bin(timestamp, 1h)
```

## Not Implemented (MVP Constraints)

| Flow | Reason |
|------|--------|
| Account Delete | Same pattern as Create/Update |
| Account Read (Event) | Typically not event-driven |
| Batch Processing | Would add batching for high volume |
| Circuit Breaker | Would use Polly in production |
| DLQ Processor | Would add in production |

---

*Document Version: 1.0*  
*Last Updated: See git history*
