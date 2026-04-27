// azure-sql.bicep
// Deploys Azure SQL Server and Northwind Database with Entra ID-only auth

@description('Base name for resources (lowercase)')
param baseName string = 'expensemgmt'

@description('Location for SQL resources')
param location string = 'uksouth'

@description('Object ID of the Entra ID administrator')
param adminObjectId string

@description('User Principal Name of the Entra ID administrator')
param adminLogin string

@description('Principal ID of the managed identity for db access')
param managedIdentityPrincipalId string

@description('Client ID of the managed identity')
param managedIdentityClientId string

@description('Name of the managed identity')
param managedIdentityName string

var uniqueSuffix = uniqueString(resourceGroup().id)
var sqlServerName = 'sql-${baseName}-${uniqueSuffix}'
var databaseName = 'Northwind'

// Azure SQL Server - Entra ID only authentication
resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: sqlServerName
  location: location
  properties: {
    // Disable SQL auth - use Entra ID only
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLogin
      principalType: 'User'
      sid: adminObjectId
      tenantId: subscription().tenantId
    }
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
  }
}

// Northwind database - Basic tier for development
resource northwindDb 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
  }
}

// Firewall rule: Allow Azure services (0.0.0.0 - 0.0.0.0)
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'AllowAllAzureIPs'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Outputs
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = northwindDb.name
output connectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${databaseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
