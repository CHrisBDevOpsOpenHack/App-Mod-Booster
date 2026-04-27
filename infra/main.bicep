// main.bicep
// Orchestrates all infrastructure deployments

@description('Base name for all resources (lowercase, no spaces)')
param baseName string = 'expensemgmt'

@description('Location for most resources')
param location string = 'uksouth'

@description('Object ID of the Entra ID administrator for SQL Server')
param adminObjectId string

@description('User Principal Name of the Entra ID administrator')
param adminLogin string

@description('Whether to deploy GenAI resources (OpenAI + AI Search)')
param deployGenAI bool = false

// App Service module (includes Managed Identity)
module appService 'app-service.bicep' = {
  name: 'appServiceDeploy'
  params: {
    baseName: baseName
    location: location
  }
}

// SQL Database module
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeploy'
  params: {
    baseName: baseName
    location: location
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
    managedIdentityClientId: appService.outputs.managedIdentityClientId
    managedIdentityName: appService.outputs.managedIdentityName
  }
}

// Update App Service with SQL connection string
module appServiceSettings 'app-service-settings.bicep' = {
  name: 'appServiceSettingsDeploy'
  dependsOn: [
    appService
    azureSql
  ]
  params: {
    webAppName: appService.outputs.webAppName
    sqlConnectionString: azureSql.outputs.connectionString
    managedIdentityClientId: appService.outputs.managedIdentityClientId
  }
}

// GenAI module (conditional)
module genAI 'genai.bicep' = if (deployGenAI) {
  name: 'genAIDeploy'
  params: {
    baseName: baseName
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
    deployGenAI: deployGenAI
  }
}

// Outputs
output webAppName string = appService.outputs.webAppName
output webAppUrl string = appService.outputs.webAppUrl
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output databaseName string = azureSql.outputs.databaseName
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityName string = appService.outputs.managedIdentityName
output openAIEndpoint string = deployGenAI ? genAI.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genAI.outputs.openAIModelName : ''
output searchEndpoint string = deployGenAI ? genAI.outputs.searchEndpoint : ''
