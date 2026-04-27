// app-service.bicep
// Deploys App Service Plan and App Service with User Assigned Managed Identity in UK South

@description('Base name for resources (lowercase)')
param baseName string = 'expensemgmt'

@description('Location for App Service resources')
param location string = 'uksouth'

@description('App Service Plan SKU')
param appServicePlanSku string = 'S1'

// Use uniqueString for deterministic unique names (no utcNow in variables)
var uniqueSuffix = uniqueString(resourceGroup().id)
var appServicePlanName = 'asp-${baseName}-${uniqueSuffix}'
var webAppName = 'app-${baseName}-${uniqueSuffix}'
var managedIdentityName = 'mid-AppModAssist-27-04-36'

// User Assigned Managed Identity
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServicePlanSku
    tier: 'Standard'
  }
  properties: {
    reserved: false
  }
}

// App Service (Web App)
resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'FtpsOnly'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: managedIdentity.properties.clientId
        }
        {
          name: 'ManagedIdentityClientId'
          value: managedIdentity.properties.clientId
        }
      ]
    }
  }
}

// Outputs
output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output managedIdentityId string = managedIdentity.id
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityName string = managedIdentity.name
// principalId is on properties for user-assigned MI
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
