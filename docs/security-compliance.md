# Security and Compliance Documentation

## Overview

This document outlines the security architecture and compliance considerations for the integration middleware. The design follows banking-grade security practices suitable for regulated environments.

## Authentication Architecture

### Managed Identity (Recommended)

I designed the solution to use **Azure Managed Identity** because:

1. **No secrets to manage**: Identity is tied to Azure resource
2. **Automatic rotation**: Azure handles credential rotation
3. **Audit trail**: Azure AD logs all token acquisitions
4. **Least privilege**: Scoped to specific resources

### Identity Configuration

| Component | Identity Type | Access To |
|-----------|--------------|-----------|
| Azure Functions | System-Assigned | Dataverse, Service Bus, Key Vault |
| Service Bus | N/A (receives from Functions) | N/A |
| Key Vault | N/A (accessed by Functions) | N/A |

### Azure AD App Registration

For Dataverse access, the Function's Managed Identity needs:

1. Register as Application User in Dataverse
2. Assign appropriate Security Role (e.g., "Integration User")
3. Grant only required entity permissions (Account: Read, Write)

```
Azure Function
     ↓ (Managed Identity)
Azure AD
     ↓ (Token with Dataverse audience)
Dataverse Web API
```

## Authorization

### API Authorization Levels

| Endpoint | Authorization | Notes |
|----------|--------------|-------|
| POST /api/accounts | Function Key + Azure AD | Create requires write permission |
| PUT /api/accounts/{id} | Function Key + Azure AD | Update requires write permission |
| Service Bus Trigger | Connection string / Managed Identity | Internal only |

### Role-Based Access (Production)

In production, I would implement:

```csharp
// Check for specific app role
if (!claimsPrincipal.HasClaim("roles", "Accounts.Write"))
{
    return Unauthorized();
}
```

Azure AD app roles:
- `Accounts.Read`: Read account data
- `Accounts.Write`: Create/update accounts
- `Accounts.Admin`: Full access including delete

## Data Protection

### Encryption in Transit

| Connection | Protocol | Minimum Version |
|------------|----------|-----------------|
| Client → Functions | HTTPS | TLS 1.2 |
| Functions → Dataverse | HTTPS | TLS 1.2 |
| Functions → Service Bus | AMQPS | TLS 1.2 |
| Functions → Key Vault | HTTPS | TLS 1.2 |
| Functions → External API | HTTPS | TLS 1.2 |

### Encryption at Rest

| Data Store | Encryption | Key Management |
|------------|------------|----------------|
| Service Bus messages | Azure SSE | Microsoft-managed |
| Function App storage | Azure SSE | Microsoft-managed |
| Key Vault secrets | Azure SSE | Customer-managed (optional) |
| Application Insights | Azure SSE | Microsoft-managed |

### PII Handling

The Account entity contains PII fields:

| Field | Classification | Production Handling |
|-------|---------------|---------------------|
| Email | PII | Encrypt at rest, mask in logs |
| Phone | PII | Encrypt at rest, mask in logs |
| Address | PII | Encrypt at rest, mask in logs |
| Name | Business Data | Log as-is |
| AccountNumber | Sensitive | May need masking |

**MVP Note**: PII encryption is not implemented in the stub code. Production would use:
- Field-level encryption before storing in Service Bus
- Azure Key Vault for encryption keys
- Log masking in Application Insights

## Key Vault Integration

### Secret Types

| Secret Name | Purpose | Rotation |
|-------------|---------|----------|
| ExternalApiKey | External system auth | 90 days |
| ServiceBusConnection | Fallback connection | 1 year |
| DataverseClientSecret | Fallback auth | 90 days |

### Access Pattern

```csharp
// In production, use Key Vault references in app settings
// local.settings.json:
{
  "ExternalApiKey": "@Microsoft.KeyVault(VaultName=kv-middleware;SecretName=ExternalApiKey)"
}
```

### Key Vault RBAC

| Principal | Role | Scope |
|-----------|------|-------|
| Function Managed Identity | Key Vault Secrets User | Key Vault |
| DevOps Pipeline | Key Vault Secrets Officer | Key Vault |
| Operations Team | Key Vault Reader | Key Vault |

## Audit and Logging

### Log Categories

| Category | Retention | Purpose |
|----------|-----------|---------|
| Security events | 1 year | Compliance |
| Business operations | 90 days | Troubleshooting |
| Performance metrics | 30 days | Optimization |
| Debug logs | 7 days | Development |

### What We Log

✅ **Always Log**:
- CorrelationId
- Timestamp (UTC)
- Operation type
- Success/failure
- Response codes
- Processing duration

⚠️ **Log with Caution**:
- Account IDs (may be sensitive)
- User identifiers

❌ **Never Log**:
- Full request/response bodies with PII
- Authentication tokens
- Secrets or keys

### Log Masking Example

```csharp
// Production logging would mask PII
_logger.LogInformation(
    "[{CorrelationId}] Processing account. Email: {Email}",
    correlationId,
    MaskEmail(account.Email)); // user@example.com → u***@e***.com
```

## Network Security

### Recommended Network Architecture (Production)

```
Internet
    ↓ (WAF/App Gateway)
Azure Functions (VNET Integration)
    ↓ (Private Endpoint)
Service Bus (Private Endpoint)
    ↓ (Private Endpoint)
Key Vault (Private Endpoint)
    ↓ (Dataverse IP restrictions)
Dynamics 365
```

### MVP Simplifications

The MVP uses public endpoints for simplicity. Production would add:
- VNET Integration for Functions
- Private Endpoints for PaaS services
- Network Security Groups
- Azure Firewall (optional)

## Compliance Considerations

### Banking Regulations

| Regulation | Relevance | Implementation Notes |
|------------|-----------|---------------------|
| GDPR | PII handling | Data encryption, retention policies |
| PCI DSS | If handling payment data | Not applicable to Account entity |
| SOC 2 | General controls | Audit logging, access controls |
| Basel III | Operational risk | Error handling, monitoring |

### Data Residency

- Azure region selection must comply with data residency requirements
- Dynamics 365 and Azure resources should be in same region when possible
- Cross-region failover needs compliance review

### Audit Requirements

For banking scenarios, ensure:

1. **Immutable audit log**: Application Insights with Log Analytics
2. **Access reviews**: Regular review of RBAC assignments
3. **Change management**: All changes through CI/CD with approvals
4. **Incident response**: Documented procedures for security events

## Threat Model Summary

### Identified Threats

| Threat | Mitigation | Status |
|--------|------------|--------|
| Unauthorized API access | Azure AD + Function Keys | ✅ Designed |
| Message tampering | Service Bus auth | ✅ Designed |
| Secret exposure | Key Vault + Managed Identity | ✅ Designed |
| PII leakage in logs | Log masking | ⚠️ Not in MVP |
| DDoS | Azure Front Door / Rate limiting | ⚠️ Not in MVP |
| Injection attacks | Input validation | ⚠️ Basic only |

### Security Testing (Production)

Before production deployment:
- [ ] Penetration testing
- [ ] Dependency vulnerability scan
- [ ] Static code analysis (SAST)
- [ ] Dynamic application security testing (DAST)

## Not Implemented (MVP Constraints)

| Security Feature | Why Omitted |
|-----------------|-------------|
| VNET Integration | Requires infrastructure |
| Private Endpoints | Requires infrastructure |
| PII Encryption | Would need Key Vault + custom code |
| Log Masking | Would need custom telemetry processor |
| Rate Limiting | Would need API Management |
| WAF | Requires infrastructure |

---

*Document Version: 1.0*  
*Last Updated: See git history*  
*Security Classification: Internal*
