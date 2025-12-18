using System.Text.Json.Serialization;

namespace IntegrationMiddleware.Functions.Models;

/*
 * ============================================================================
 * AccountDto.cs - Account Data Transfer Object
 * ============================================================================
 * 
 * DESIGN DECISIONS:
 * -----------------
 * This DTO represents the Account entity data as it flows through the middleware.
 * I intentionally kept this minimal for the MVP scope.
 * 
 * Field selection rationale:
 * - AccountId: Primary key from Dataverse (required for idempotency)
 * - AccountNumber: Business identifier (required for external system mapping)
 * - Name: Display name (required for most operations)
 * - Status: Account lifecycle state (important for business logic)
 * - Email/Phone: Contact details (common integration fields)
 * - Address fields: Minimal set for typical CRM-to-backend sync
 * 
 * BANKING/COMPLIANCE NOTES:
 * -------------------------
 * - Email and Phone are PII - would need encryption at rest in production
 * - Address fields are PII - same treatment required
 * - AccountNumber may be sensitive depending on format
 * 
 * MAPPING NOTES:
 * --------------
 * Dataverse field names differ from this DTO. The DataverseService handles
 * the mapping between Dataverse schema names (e.g., "accountnumber") and
 * this DTO's property names.
 * 
 * OUT OF SCOPE:
 * -------------
 * - Financial fields (credit limit, payment terms)
 * - Related entities (contacts, opportunities)
 * - Custom fields specific to banking domain
 * - Full address normalization
 */

/// <summary>
/// Data transfer object representing an Account entity.
/// Used for both Dataverse operations and external API calls.
/// </summary>
public class AccountDto
{
    /// <summary>
    /// Dataverse record GUID. Maps to 'accountid' in Dataverse.
    /// </summary>
    [JsonPropertyName("accountId")]
    public Guid AccountId { get; set; }

    /// <summary>
    /// Business account number. Maps to 'accountnumber' in Dataverse.
    /// </summary>
    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }

    /// <summary>
    /// Account display name. Maps to 'name' in Dataverse.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Account status. Maps to 'statecode' in Dataverse (0=Active, 1=Inactive).
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// Primary email address. Maps to 'emailaddress1' in Dataverse.
    /// PII - requires encryption at rest in production.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>
    /// Primary phone number. Maps to 'telephone1' in Dataverse.
    /// PII - requires encryption at rest in production.
    /// </summary>
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// Street address line 1. Maps to 'address1_line1' in Dataverse.
    /// </summary>
    [JsonPropertyName("addressLine1")]
    public string? AddressLine1 { get; set; }

    /// <summary>
    /// City. Maps to 'address1_city' in Dataverse.
    /// </summary>
    [JsonPropertyName("city")]
    public string? City { get; set; }

    /// <summary>
    /// State/Province. Maps to 'address1_stateorprovince' in Dataverse.
    /// </summary>
    [JsonPropertyName("stateProvince")]
    public string? StateProvince { get; set; }

    /// <summary>
    /// Postal code. Maps to 'address1_postalcode' in Dataverse.
    /// </summary>
    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    /// <summary>
    /// Country. Maps to 'address1_country' in Dataverse.
    /// </summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>
    /// Record created timestamp from Dataverse. Maps to 'createdon'.
    /// </summary>
    [JsonPropertyName("createdOn")]
    public DateTime? CreatedOn { get; set; }

    /// <summary>
    /// Record last modified timestamp from Dataverse. Maps to 'modifiedon'.
    /// </summary>
    [JsonPropertyName("modifiedOn")]
    public DateTime? ModifiedOn { get; set; }
}
