# Dynamics 365 CE Integration Middleware

[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Azure Functions](https://img.shields.io/badge/Azure%20Functions-v4-blue)](https://azure.microsoft.com/services/functions/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

## What This Solution Is

This is an **Azure-based integration middleware** that connects Microsoft Dynamics 365 CE (Dataverse) to external systems. It demonstrates enterprise integration patterns suitable for banking and regulated environments.

**Context**: This solution was developed as a 4-hour offline coding test demonstrating senior-level integration architecture skills.

## Architecture Overview

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Dynamics 365   │────▶│  Azure Service  │────▶│ Azure Functions │
│     CE          │     │      Bus        │     │   (.NET 8)      │
│   (Dataverse)   │     │                 │     │                 │
└─────────────────┘     └─────────────────┘     └────────┬────────┘
                                                         │
                                                         ▼
                                                ┌─────────────────┐
                                                │ External System │
                                                │   (REST API)    │
                                                └─────────────────┘
```

### Key Components

| Component | Technology | Purpose |
|-----------|------------|---------|
| Event Processor | Azure Function (ServiceBusTrigger) | Processes account events from D365 |
| CRUD API | Azure Function (HttpTrigger) | REST API for account operations |
| Message Broker | Azure Service Bus | Reliable async messaging |
| Source System | Dynamics 365 CE | CRM data source |
| Target System | External REST API | Core banking/ERP sync |

## Integration Flow Summary

### Event-Driven Flow (Create/Update Account)

1. User creates/updates Account in Dynamics 365
2. Dataverse plugin publishes event to Service Bus
3. Azure Function processes message
4. Function validates and syncs to external system
5. Message completed or retried on failure

### HTTP API Flow

- `POST /api/accounts` - Create account in Dataverse
- `PUT /api/accounts/{id}` - Update account in Dataverse

## Repository Structure

```
/
├── IntegrationMiddleware.sln          # Visual Studio solution
├── src/
│   └── IntegrationMiddleware.Functions/
│       ├── Functions/
│       │   ├── AccountEventProcessor.cs  # Service Bus trigger
│       │   └── AccountCrudApi.cs          # HTTP CRUD API
│       ├── Models/
│       │   ├── AccountEvent.cs            # Message contract
│       │   └── AccountDto.cs              # Data transfer object
│       ├── Services/
│       │   ├── DataverseService.cs        # Dataverse Web API client
│       │   └── ExternalApiService.cs      # External system client
│       ├── Program.cs                     # DI and host configuration
│       └── host.json                      # Function host settings
├── docs/
│   ├── architecture.md / .docx
│   ├── integration-flow.md / .docx
│   └── security-compliance.md / .docx
├── diagrams/
│   ├── architecture.d2
│   └── sequence-account.d2
├── config/
│   └── appsettings.sample.json
└── README.md
```

## Building and Running

### Prerequisites

- .NET 8 SDK
- Visual Studio 2022 or VS Code with C# extension
- Azure Functions Core Tools v4 (for local development)

### Build

```bash
# Command line
dotnet build IntegrationMiddleware.sln

# Or open IntegrationMiddleware.sln in Visual Studio
```

### Local Development

1. Copy `config/appsettings.sample.json` to `src/IntegrationMiddleware.Functions/local.settings.json`
2. Update configuration values
3. Run with Azure Functions Core Tools:

```bash
cd src/IntegrationMiddleware.Functions
func start
```

## What Is Mocked/Simplified

I intentionally simplified the following for the 4-hour time constraint:

| Component | What's Mocked | Production Implementation |
|-----------|--------------|---------------------------|
| DataverseService | Returns mock data | Actual HTTP calls with Managed Identity |
| ExternalApiService | Returns success | Actual HTTP calls with OAuth/API key |
| Authentication | Function keys only | Azure AD + RBAC |
| Idempotency | Not implemented | Redis/Cosmos for deduplication |
| Resilience | Basic retry | Polly with circuit breaker |

### Why I Made These Simplifications

1. **Focus on patterns over plumbing**: The architectural decisions and code structure matter more than actual HTTP implementation
2. **Demonstrable without infrastructure**: You can open, build, and understand the solution without Azure resources
3. **Time constraint**: 4 hours requires prioritization

## What I Would Add With More Time

### High Priority (Next 2-4 hours)

- [ ] **Unit tests**: xUnit tests for services and functions
- [ ] **Integration tests**: TestServer for HTTP functions
- [ ] **Polly resilience**: Retry, circuit breaker, timeout policies
- [ ] **Full CRUD**: GET and DELETE operations
- [ ] **Input validation**: FluentValidation for request DTOs

### Medium Priority (Next day)

- [ ] **Idempotency service**: Redis-based duplicate detection
- [ ] **OpenAPI/Swagger**: API documentation
- [ ] **Health checks**: Liveness and readiness probes
- [ ] **DLQ processor**: Handle poison messages
- [ ] **Infrastructure as Code**: Bicep templates

### Lower Priority (Production readiness)

- [ ] **VNET integration**: Private networking
- [ ] **PII encryption**: Field-level encryption
- [ ] **Log masking**: Custom Application Insights processor
- [ ] **Rate limiting**: API Management layer
- [ ] **Performance tests**: Load testing scenarios

## Design Decisions & Trade-offs

### Why Service Bus over Event Grid?

I chose Service Bus because:
- **Guaranteed delivery** with explicit acknowledgment
- **Dead Letter Queue** for poison message handling
- **Message sessions** for ordered processing if needed

Event Grid is better for fan-out/pub-sub where we don't need guaranteed per-message processing.

### Why Azure Functions over Logic Apps?

I chose Functions because:
- **Code-first** gives more control and testability
- **Standard .NET patterns** for enterprise development
- **Lower latency** for high-volume scenarios
- **Version control friendly** (no designer-generated JSON)

Logic Apps are better for low-code orchestration with visual design.

### Why Isolated Worker Model?

I chose the .NET 8 Isolated Worker model because:
- **Future-proof**: In-process model is deprecated
- **Full .NET control**: Own process, own dependencies
- **Middleware support**: Custom cross-cutting concerns

### Why Stub Implementations?

For a 4-hour coding test, I prioritized:
- **Demonstrable architecture**: Clear patterns and structure
- **Buildable solution**: Opens and compiles in Visual Studio
- **Readable code**: Self-documenting with extensive comments

The stub implementations show exactly what production code would do, with clear `// STUB IMPLEMENTATION` blocks showing the real implementation approach.

## Security Considerations

This solution is designed for banking-grade security:

| Aspect | Approach |
|--------|----------|
| Authentication | Managed Identity (no secrets) |
| Authorization | Azure AD + RBAC |
| Encryption (transit) | TLS 1.2+ |
| Encryption (at rest) | Azure SSE |
| Secrets | Key Vault references |
| Audit | CorrelationId in all logs |
| PII | Would encrypt/mask in production |

See [docs/security-compliance.md](docs/security-compliance.md) for full details.

## Interview Talking Points

If asked about this solution, I would highlight:

1. **Pattern selection**: Why event-driven with Service Bus (guaranteed delivery, DLQ)
2. **Technology choices**: .NET 8 Isolated Functions (future-proof, testable)
3. **Security by design**: Managed Identity eliminates secrets management
4. **Pragmatic MVP**: Focused on demonstrating skills, not feature completeness
5. **Production awareness**: Comments explain what production would add

## Contributing

This is a demonstration project for interview purposes.

## License

MIT License - See LICENSE file for details.

---

*Built as a 4-hour offline coding test demonstrating Dynamics 365 / Azure integration architecture.*