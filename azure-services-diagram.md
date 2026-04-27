# Azure Services Diagram - Expense Management System

## Service Architecture

```mermaid
graph TB
    subgraph Internet
        User[👤 User Browser]
    end

    subgraph Azure["Azure (UK South)"]
        subgraph AppTier["App Tier"]
            AS[🌐 Azure App Service\napp-expensemgmt-xxx\nS1 SKU / net8.0]
            MI[🔑 User Assigned\nManaged Identity\nmid-AppModAssist-27-04-36]
        end

        subgraph DataTier["Data Tier"]
            SQL[🗄️ Azure SQL Database\nNorthwind\nBasic Tier\nEntra ID Only Auth]
        end

        subgraph GenAI["GenAI Services (Sweden Central)"]
            AOAI[🤖 Azure OpenAI\nGPT-4o Model\nS0 SKU]
            SRCH[🔍 AI Search\nbasic SKU]
        end
    end

    subgraph "Deployment Tools"
        DevMachine[💻 Developer Machine\naz cli + Python]
    end

    User -->|HTTPS| AS
    AS -->|Managed Identity Auth| SQL
    AS -->|Managed Identity Auth| AOAI
    AS -->|Managed Identity Auth| SRCH
    MI -.->|Assigned To| AS
    MI -.->|Role: db_datareader\ndb_datawriter\nEXECUTE| SQL
    MI -.->|Role: Cognitive Services\nOpenAI User| AOAI
    MI -.->|Role: Search Index\nData Contributor| SRCH
    DevMachine -->|az deployment group create| AS
    DevMachine -->|python3 run-sql.py| SQL
    DevMachine -->|az webapp deploy app.zip| AS
```

## Services Summary

| Service | Name Pattern | SKU | Region | Purpose |
|---------|-------------|-----|--------|---------|
| App Service Plan | `asp-expensemgmt-xxx` | S1 Standard | UK South | Hosts the web app |
| App Service | `app-expensemgmt-xxx` | - | UK South | ASP.NET Razor Pages + REST API |
| Managed Identity | `mid-AppModAssist-27-04-36` | User Assigned | UK South | Passwordless auth to all services |
| Azure SQL Server | `sql-expensemgmt-xxx` | - | UK South | SQL Server (Entra ID only) |
| Azure SQL Database | `Northwind` | Basic | UK South | Expense data storage |
| Azure OpenAI | `oai-expensemgmt-xxx` | S0 | Sweden Central | GPT-4o for AI assistant |
| AI Search | `srch-expensemgmt-xxx` | Basic | UK South | RAG pattern for chat |

## Connection Flow

```
Browser → App Service → SQL Database
                     → Azure OpenAI (for /chatui)
                     → AI Search (for /chatui RAG)

All authentication uses Managed Identity (no passwords or keys stored)
```

## Security Model

- **No SQL passwords**: Entra ID only authentication enforced
- **No API keys**: All Azure service connections use Managed Identity  
- **HTTPS only**: App Service configured with `httpsOnly: true`
- **TLS 1.2+**: Minimum TLS version enforced
- **Least privilege**: MI granted only required roles on each service

## Deployment Scripts

```bash
# Deploy without GenAI (app + database only)
chmod +x deploy.sh && ./deploy.sh

# Deploy with GenAI (full stack including OpenAI + AI Search)
chmod +x deploy-with-chat.sh && ./deploy-with-chat.sh
```

## Application URLs

After deployment, the app is accessible at:
- **Main UI**: `https://<app-name>.azurewebsites.net/Index`
- **API Docs**: `https://<app-name>.azurewebsites.net/swagger`
- **AI Assistant**: `https://<app-name>.azurewebsites.net/chatui`
