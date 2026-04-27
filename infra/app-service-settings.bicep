// app-service-settings.bicep
// Updates App Service application settings with SQL connection string
// This is separate to avoid circular dependencies

@description('Name of the existing App Service')
param webAppName string

@description('SQL Server connection string (without auth - auth handled by managed identity)')
param sqlConnectionString string

@description('Client ID of the managed identity')
param managedIdentityClientId string

resource webApp 'Microsoft.Web/sites@2022-09-01' existing = {
  name: webAppName
}

resource webAppSettings 'Microsoft.Web/sites/config@2022-09-01' = {
  parent: webApp
  name: 'appsettings'
  properties: {
    ASPNETCORE_ENVIRONMENT: 'Production'
    AZURE_CLIENT_ID: managedIdentityClientId
    ManagedIdentityClientId: managedIdentityClientId
    ConnectionStrings__DefaultConnection: sqlConnectionString
  }
}
