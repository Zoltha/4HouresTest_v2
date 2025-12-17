using System.Text.Json.Serialization;

namespace IntegrationMiddleware.Functions.Models;

/*
 * ============================================================================
 * AccountEvent.cs - Service Bus Message Contract
 * ============================================================================
 * 
 * DESIGN DECISIONS:
 * -----------------
 * This represents the message contract published to Azure Service Bus when
 * an Account entity is created or updated in Dynamics 365 Dataverse.
 * 
 * I kept this intentionally simple for the MVP:
 * - Only essential fields needed for downstream processing
 * - EventType to distinguish Create vs Update
 * - CorrelationId for distributed tracing (critical for banking)
 * - Timestamp for audit and ordering
 * 
 * BANKING/COMPLIANCE NOTES:
 * -------------------------
 * - CorrelationId is mandatory for audit trail and troubleshooting
 * - Timestamp should be UTC to avoid timezone issues
 * - AccountNumber is business-sensitive but not PII
 * 
 * OUT OF SCOPE:
 * -------------
 * - Full change tracking (before/after values)
 * - Message versioning schema
 * - Compression for large payloads
 */

/// <summary>
/// Represents an account event message from Dynamics 365 Dataverse.
/// This is the contract published to Azure Service Bus.
/// </summary>
public class AccountEvent
{
    /// <summary>
    /// Unique identifier for distributed tracing across systems.
    /// Critical for banking audit requirements.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of event: "Create" or "Update"
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the event occurred in source system.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Dataverse entity logical name (always "account" for this event).
    /// </summary>
    [JsonPropertyName("entityName")]
    public string EntityName { get; set; } = "account";

    /// <summary>
    /// Dataverse record GUID.
    /// </summary>
    [JsonPropertyName("entityId")]
    public Guid EntityId { get; set; }

    /// <summary>
    /// Account payload data.
    /// </summary>
    [JsonPropertyName("data")]
    public AccountDto? Data { get; set; }
}

/// <summary>
/// Valid event types for account events.
/// </summary>
public static class EventTypes
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete"; // Not implemented in MVP
}
