#!/usr/bin/env bash
# deploy-with-chat.sh
# Deploys everything: App Service, SQL Database, GenAI (Azure OpenAI + AI Search), and application code
#
# Prerequisites:
#   - az cli installed and logged in: az login
#   - jq installed
#   - ODBC Driver 18 for SQL Server installed
#
# Usage:
#   ./deploy-with-chat.sh
#
# One-line quick start:
#   chmod +x deploy-with-chat.sh && ./deploy-with-chat.sh

set -euo pipefail

# ============================================================
# CONFIGURATION - Update these values before running
# ============================================================
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"
BASE_NAME="expensemgmt"

# Get current user's Entra ID Object ID and UPN
echo "Getting current user information from Azure AD..."
ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv 2>/dev/null || echo "")
ADMIN_LOGIN=$(az ad signed-in-user show --query userPrincipalName -o tsv 2>/dev/null || echo "")

if [ -z "$ADMIN_OBJECT_ID" ]; then
    echo "ERROR: Could not get current user Object ID. Please run 'az login' first."
    exit 1
fi

echo "Admin Object ID: $ADMIN_OBJECT_ID"
echo "Admin Login: $ADMIN_LOGIN"

# ============================================================
# STEP 1: Create Resource Group
# ============================================================
echo ""
echo "=========================================="
echo "STEP 1: Creating resource group..."
echo "=========================================="
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --output none

echo "✓ Resource group '$RESOURCE_GROUP' ready"

# ============================================================
# STEP 2: Deploy Infrastructure (including GenAI)
# ============================================================
echo ""
echo "=========================================="
echo "STEP 2: Deploying full infrastructure (App Service + SQL + GenAI)..."
echo "Note: Azure OpenAI deploys to swedencentral for GPT-4o availability"
echo "=========================================="

DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file infra/main.bicep \
    --parameters \
        baseName="$BASE_NAME" \
        location="$LOCATION" \
        adminObjectId="$ADMIN_OBJECT_ID" \
        adminLogin="$ADMIN_LOGIN" \
        deployGenAI=true \
    --query properties.outputs \
    --output json)

echo "$DEPLOYMENT_OUTPUT" | jq '.'

# Extract deployment outputs
WEB_APP_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.webAppName.value')
WEB_APP_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.webAppUrl.value')
SQL_SERVER_FQDN=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.sqlServerFqdn.value')
DATABASE_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.databaseName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityClientId.value')
MANAGED_IDENTITY_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityName.value')

# GenAI outputs
OPENAI_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.openAIEndpoint.value // empty')
OPENAI_MODEL_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.openAIModelName.value // "gpt-4o"')
SEARCH_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.searchEndpoint.value // empty')

echo ""
echo "✓ Deployment complete:"
echo "  Web App: $WEB_APP_NAME"
echo "  URL: $WEB_APP_URL"
echo "  SQL Server: $SQL_SERVER_FQDN"
echo "  OpenAI Endpoint: $OPENAI_ENDPOINT"
echo "  Search Endpoint: $SEARCH_ENDPOINT"

# ============================================================
# STEP 3: Configure App Service Settings (including GenAI endpoints)
# ============================================================
echo ""
echo "=========================================="
echo "STEP 3: Configuring App Service settings (including GenAI)..."
echo "=========================================="

CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN},1433;Initial Catalog=${DATABASE_NAME};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

az webapp config appsettings set \
    --name "$WEB_APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --settings \
        "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
        "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
        "ConnectionStrings__DefaultConnection=$CONNECTION_STRING" \
        "ASPNETCORE_ENVIRONMENT=Production" \
        "GenAISettings__OpenAIEndpoint=$OPENAI_ENDPOINT" \
        "GenAISettings__OpenAIDeploymentName=$OPENAI_MODEL_NAME" \
        "GenAISettings__SearchEndpoint=$SEARCH_ENDPOINT" \
        "GenAISettings__SearchIndexName=expense-index" \
        "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
        "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
    --output none

echo "✓ App Service settings configured (including GenAI endpoints)"

# ============================================================
# STEP 4: Wait for SQL Server
# ============================================================
echo ""
echo "=========================================="
echo "STEP 4: Waiting 30 seconds for SQL Server to be fully ready..."
echo "=========================================="
sleep 30

# ============================================================
# STEP 5: Add current IP and Azure services to SQL Firewall
# ============================================================
echo ""
echo "=========================================="
echo "STEP 5: Configuring SQL firewall rules..."
echo "=========================================="

MY_IP=$(curl -s https://api.ipify.org)
SQL_SERVER_NAME=$(echo "$SQL_SERVER_FQDN" | cut -d'.' -f1)

echo "Your IP: $MY_IP"

# Allow Azure services access
az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --name "AllowAllAzureIPs" \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0 \
    --output none

# Add deployment IP
az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --name "AllowDeploymentIP" \
    --start-ip-address "$MY_IP" \
    --end-ip-address "$MY_IP" \
    --output none

echo "Waiting additional 15 seconds for firewall rules to propagate..."
sleep 15
echo "✓ Firewall rules configured"

# ============================================================
# STEP 6: Update Python scripts with correct server/database
# ============================================================
echo ""
echo "=========================================="
echo "STEP 6: Updating Python script configurations..."
echo "=========================================="

sed -i.bak "s|SERVER = \".*database.windows.net\"|SERVER = \"${SQL_SERVER_FQDN}\"|g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s|DATABASE = \".*\"|DATABASE = \"${DATABASE_NAME}\"|g" run-sql.py && rm -f run-sql.py.bak

sed -i.bak "s|SERVER = \".*database.windows.net\"|SERVER = \"${SQL_SERVER_FQDN}\"|g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s|DATABASE = \".*\"|DATABASE = \"${DATABASE_NAME}\"|g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak

sed -i.bak "s|SERVER = \".*database.windows.net\"|SERVER = \"${SQL_SERVER_FQDN}\"|g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
sed -i.bak "s|DATABASE = \".*\"|DATABASE = \"${DATABASE_NAME}\"|g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak

sed -i.bak "s/MANAGED-IDENTITY-NAME/$MANAGED_IDENTITY_NAME/g" script.sql && rm -f script.sql.bak

echo "✓ Scripts updated"

# ============================================================
# STEP 7: Install Python dependencies
# ============================================================
echo ""
echo "=========================================="
echo "STEP 7: Installing Python dependencies..."
echo "=========================================="
pip3 install --quiet pyodbc azure-identity
echo "✓ Python packages installed"

# ============================================================
# STEP 8: Import database schema
# ============================================================
echo ""
echo "=========================================="
echo "STEP 8: Importing database schema..."
echo "=========================================="
python3 run-sql.py
echo "✓ Database schema imported"

# ============================================================
# STEP 9: Configure database roles for managed identity
# ============================================================
echo ""
echo "=========================================="
echo "STEP 9: Configuring database roles for managed identity..."
echo "=========================================="
python3 run-sql-dbrole.py
echo "✓ Database roles configured"

# ============================================================
# STEP 10: Deploy stored procedures
# ============================================================
echo ""
echo "=========================================="
echo "STEP 10: Deploying stored procedures..."
echo "=========================================="
python3 run-sql-stored-procs.py
echo "✓ Stored procedures deployed"

# ============================================================
# STEP 11: Deploy application code
# ============================================================
echo ""
echo "=========================================="
echo "STEP 11: Deploying application code..."
echo "=========================================="

if [ ! -f "app.zip" ]; then
    echo "Building and packaging application..."
    cd app/ExpenseManager
    dotnet publish -c Release -o ./publish
    cd ./publish
    zip -r ../../../app.zip . --exclude "*.pdb"
    cd ../../..
    echo "✓ app.zip created"
fi

echo "Deploying app.zip to Azure App Service..."
az webapp deploy \
    --resource-group "$RESOURCE_GROUP" \
    --name "$WEB_APP_NAME" \
    --src-path ./app.zip \
    --type zip

echo "✓ Application deployed"

# ============================================================
# SUMMARY
# ============================================================
echo ""
echo "=========================================="
echo "✅ FULL DEPLOYMENT COMPLETE (including GenAI)!"
echo "=========================================="
echo ""
echo "🌐 Application URL: ${WEB_APP_URL}/Index"
echo "📚 API Docs: ${WEB_APP_URL}/swagger"
echo "💬 AI Assistant: ${WEB_APP_URL}/chatui"
echo ""
echo "Azure OpenAI Endpoint: $OPENAI_ENDPOINT"
echo "AI Search Endpoint: $SEARCH_ENDPOINT"
echo ""
