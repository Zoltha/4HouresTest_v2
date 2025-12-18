using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IntegrationMiddleware.Functions.Services;

/*
 * ============================================================================
 * Program.cs - Azure Functions Isolated Worker Host Configuration
 * ============================================================================
 * 
 * DESIGN DECISIONS:
 * -----------------
 * I chose the Azure Functions Isolated Worker model (.NET 8) because:
 * 1. It provides better control over dependencies and middleware
 * 2. It's the recommended model going forward (in-process is deprecated)
 * 3. It allows for cleaner separation of concerns in DI
 * 
 * BANKING/SECURITY CONSIDERATIONS:
 * --------------------------------
 * - In production, I would add Application Insights for distributed tracing
 * - HTTP clients use resilience patterns (Polly) for external calls
 * - All services are registered as interfaces for testability
 * 
 * OUT OF SCOPE (4-hour constraint):
 * ---------------------------------
 * - Polly resilience policies (would add retry, circuit breaker, timeout)
 * - Health check endpoints
 * - Custom telemetry processors
 */

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Register HttpClient factory for external API calls
        // In production: Add Polly policies for retry, circuit breaker, timeout
        services.AddHttpClient<IExternalApiService, ExternalApiService>(client =>
        {
            // Base configuration - actual URL comes from configuration
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Register Dataverse service
        // In production: This would use Azure.Identity ManagedIdentityCredential
        services.AddHttpClient<IDataverseService, DataverseService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            client.DefaultRequestHeaders.Add("OData-Version", "4.0");
        });

        // =================================================================
        // APPLICATION INSIGHTS (NOT IMPLEMENTED - MVP)
        // =================================================================
        // In production, Application Insights would be configured via:
        // - APPLICATIONINSIGHTS_CONNECTION_STRING environment variable
        // - The isolated worker model automatically integrates with App Insights
        // when the connection string is provided in host.json or app settings.
        //
        // Additional setup for advanced scenarios:
        // services.AddApplicationInsightsTelemetryWorkerService();
        // services.ConfigureFunctionsApplicationInsights();
        // =================================================================

        services.AddLogging(logging =>
        {
            logging.AddFilter("Microsoft", LogLevel.Warning);
            logging.AddFilter("System", LogLevel.Warning);
            logging.AddFilter("IntegrationMiddleware", LogLevel.Information);
        });
    })
    .Build();

host.Run();
