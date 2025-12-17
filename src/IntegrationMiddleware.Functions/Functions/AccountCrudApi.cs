using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using IntegrationMiddleware.Functions.Models;
using IntegrationMiddleware.Functions.Services;

namespace IntegrationMiddleware.Functions.Functions;

/*
 * ============================================================================
 * AccountCrudApi.cs - HTTP-triggered Azure Functions for Account CRUD
 * ============================================================================
 * 
 * DESIGN DECISIONS:
 * -----------------
 * I implemented Create and Update operations only for this MVP because:
 * 1. They are the most common integration patterns
 * 2. They demonstrate the key architectural concepts
 * 3. Read and Delete follow the same patterns (easy to add)
 * 
 * INTENTIONALLY NOT IMPLEMENTED:
 * - GET (Read): Would follow same pattern, omitted for time
 * - DELETE: Would follow same pattern, omitted for time
 * - LIST/Query: Would add OData-style filtering, omitted for time
 * 
 * AUTHENTICATION/AUTHORIZATION:
 * -----------------------------
 * In production, these endpoints would be secured with:
 * 
 * 1. AZURE AD AUTHENTICATION:
 *    - Function App configured with Azure AD authentication
 *    - Callers must present valid Azure AD token
 *    - Token validated against specific app registration
 *    
 * 2. AUTHORIZATION:
 *    - Role-based access (app roles in Azure AD)
 *    - Scope validation for specific operations
 *    
 * 3. API MANAGEMENT (optional but recommended):
 *    - Rate limiting
 *    - Request/response transformation
 *    - API key or subscription key
 *    - Caching
 * 
 * MANAGED IDENTITY:
 * -----------------
 * The Function App would use Managed Identity to access:
 * - Dataverse (via Azure AD token)
 * - Key Vault (for any secrets)
 * - Service Bus (for publishing events)
 * 
 * VALIDATION:
 * -----------
 * Production would include:
 * - FluentValidation or DataAnnotations
 * - Input sanitization
 * - Business rule validation
 * 
 * BANKING/SECURITY NOTES:
 * -----------------------
 * - All requests logged with correlation ID
 * - PII would be masked in logs
 * - Rate limiting to prevent abuse
 * - Input validation to prevent injection attacks
 */

public class AccountCrudApi
{
    private readonly IDataverseService _dataverseService;
    private readonly ILogger<AccountCrudApi> _logger;

    public AccountCrudApi(
        IDataverseService dataverseService,
        ILogger<AccountCrudApi> logger)
    {
        _dataverseService = dataverseService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new account in Dataverse.
    /// </summary>
    /// <remarks>
    /// POST /api/accounts
    /// 
    /// AUTHENTICATION (not implemented in MVP):
    /// - Azure AD token required in Authorization header
    /// - Token must have "Accounts.Write" scope
    /// 
    /// REQUEST BODY:
    /// {
    ///   "name": "Account Name",
    ///   "accountNumber": "ACC-001",
    ///   "email": "contact@example.com",
    ///   "phone": "+1-555-0100"
    /// }
    /// 
    /// RESPONSE:
    /// 201 Created with Location header and created account
    /// 400 Bad Request if validation fails
    /// 401 Unauthorized if not authenticated
    /// 403 Forbidden if not authorized
    /// </remarks>
    [Function(nameof(CreateAccount))]
    public async Task<HttpResponseData> CreateAccount(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "accounts")] 
        HttpRequestData req)
    {
        // Generate correlation ID for this request
        var correlationId = req.Headers.TryGetValues("X-Correlation-Id", out var values)
            ? values.First()
            : Guid.NewGuid().ToString();

        _logger.LogInformation(
            "[{CorrelationId}] Create account request received",
            correlationId);

        try
        {
            // =================================================================
            // AUTHENTICATION CHECK (NOT IMPLEMENTED - MVP)
            // =================================================================
            // In production:
            // var claimsPrincipal = req.FunctionContext.GetClaimsPrincipal();
            // if (claimsPrincipal?.Identity?.IsAuthenticated != true)
            // {
            //     return CreateErrorResponse(req, HttpStatusCode.Unauthorized,
            //         "Authentication required", correlationId);
            // }
            // 
            // if (!claimsPrincipal.HasClaim("scope", "Accounts.Write"))
            // {
            //     return CreateErrorResponse(req, HttpStatusCode.Forbidden,
            //         "Insufficient permissions", correlationId);
            // }
            // =================================================================

            // Deserialize request body
            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                    "Request body is required", correlationId);
            }

            var account = JsonSerializer.Deserialize<AccountDto>(requestBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (account == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                    "Invalid account data", correlationId);
            }

            // =================================================================
            // VALIDATION (SIMPLIFIED - MVP)
            // =================================================================
            // Production would use FluentValidation or DataAnnotations
            if (string.IsNullOrWhiteSpace(account.Name))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                    "Account name is required", correlationId);
            }

            // Create account in Dataverse
            var accountId = await _dataverseService.CreateAccountAsync(account, correlationId);
            account.AccountId = accountId;
            account.CreatedOn = DateTime.UtcNow;
            account.ModifiedOn = DateTime.UtcNow;

            _logger.LogInformation(
                "[{CorrelationId}] Account created successfully. AccountId: {AccountId}",
                correlationId, accountId);

            // Return 201 Created with the created account
            var response = req.CreateResponse(HttpStatusCode.Created);
            response.Headers.Add("Content-Type", "application/json");
            response.Headers.Add("X-Correlation-Id", correlationId);
            response.Headers.Add("Location", $"/api/accounts/{accountId}");
            
            await response.WriteStringAsync(JsonSerializer.Serialize(account,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "[{CorrelationId}] Invalid JSON in request body",
                correlationId);
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                "Invalid JSON format", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] Error creating account",
                correlationId);
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError,
                "An error occurred while creating the account", correlationId);
        }
    }

    /// <summary>
    /// Updates an existing account in Dataverse.
    /// </summary>
    /// <remarks>
    /// PUT /api/accounts/{id}
    /// 
    /// AUTHENTICATION (not implemented in MVP):
    /// - Azure AD token required in Authorization header
    /// - Token must have "Accounts.Write" scope
    /// 
    /// REQUEST BODY:
    /// {
    ///   "name": "Updated Account Name",
    ///   "email": "updated@example.com"
    /// }
    /// 
    /// RESPONSE:
    /// 200 OK with updated account
    /// 400 Bad Request if validation fails
    /// 404 Not Found if account doesn't exist
    /// 401 Unauthorized if not authenticated
    /// 403 Forbidden if not authorized
    /// </remarks>
    [Function(nameof(UpdateAccount))]
    public async Task<HttpResponseData> UpdateAccount(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "accounts/{id:guid}")] 
        HttpRequestData req,
        Guid id)
    {
        // Generate correlation ID for this request
        var correlationId = req.Headers.TryGetValues("X-Correlation-Id", out var values)
            ? values.First()
            : Guid.NewGuid().ToString();

        _logger.LogInformation(
            "[{CorrelationId}] Update account request received. AccountId: {AccountId}",
            correlationId, id);

        try
        {
            // =================================================================
            // AUTHENTICATION CHECK (NOT IMPLEMENTED - MVP)
            // Same pattern as CreateAccount
            // =================================================================

            // Deserialize request body
            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                    "Request body is required", correlationId);
            }

            var accountUpdate = JsonSerializer.Deserialize<AccountDto>(requestBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (accountUpdate == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                    "Invalid account data", correlationId);
            }

            // =================================================================
            // VALIDATION (SIMPLIFIED - MVP)
            // =================================================================
            if (string.IsNullOrWhiteSpace(accountUpdate.Name))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                    "Account name is required", correlationId);
            }

            // Set the ID from the route parameter
            accountUpdate.AccountId = id;
            accountUpdate.ModifiedOn = DateTime.UtcNow;

            // =================================================================
            // CHECK IF ACCOUNT EXISTS (SIMPLIFIED - MVP)
            // =================================================================
            // In production, Dataverse PATCH would return 404 if not found
            // We'd handle that response appropriately
            var existingAccount = await _dataverseService.GetAccountAsync(id, correlationId);
            if (existingAccount == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.NotFound,
                    $"Account {id} not found", correlationId);
            }

            // Update account in Dataverse
            await _dataverseService.UpdateAccountAsync(accountUpdate, correlationId);

            _logger.LogInformation(
                "[{CorrelationId}] Account updated successfully. AccountId: {AccountId}",
                correlationId, id);

            // Return 200 OK with the updated account
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            response.Headers.Add("X-Correlation-Id", correlationId);
            
            await response.WriteStringAsync(JsonSerializer.Serialize(accountUpdate,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "[{CorrelationId}] Invalid JSON in request body",
                correlationId);
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                "Invalid JSON format", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] Error updating account {AccountId}",
                correlationId, id);
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError,
                "An error occurred while updating the account", correlationId);
        }
    }

    /// <summary>
    /// Creates a standardized error response.
    /// </summary>
    private static async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData req,
        HttpStatusCode statusCode,
        string message,
        string correlationId)
    {
        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json");
        response.Headers.Add("X-Correlation-Id", correlationId);
        
        var errorBody = JsonSerializer.Serialize(new
        {
            error = message,
            correlationId = correlationId,
            timestamp = DateTime.UtcNow
        });
        
        await response.WriteStringAsync(errorBody);
        return response;
    }
}

/*
 * ============================================================================
 * INTENTIONALLY NOT IMPLEMENTED (MVP - 4 HOUR CONSTRAINT)
 * ============================================================================
 * 
 * 1. GET /api/accounts/{id}
 *    - Would call _dataverseService.GetAccountAsync(id, correlationId)
 *    - Return 200 OK with account, or 404 Not Found
 * 
 * 2. DELETE /api/accounts/{id}
 *    - Would call _dataverseService.DeleteAccountAsync(id, correlationId)
 *    - Soft delete vs hard delete decision (banking typically soft delete)
 *    - Return 204 No Content, or 404 Not Found
 * 
 * 3. GET /api/accounts (List with filtering)
 *    - Would support OData-style query parameters
 *    - $filter, $orderby, $top, $skip
 *    - Pagination with continuation tokens
 * 
 * 4. PATCH /api/accounts/{id} (Partial update)
 *    - Would allow updating specific fields only
 *    - More efficient for large entities
 * 
 * ============================================================================
 */
