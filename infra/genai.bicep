// genai.bicep
// Deploys Azure OpenAI and AI Search resources
// NOTE: Azure OpenAI MUST be in swedencentral for GPT-4o availability

@description('Base name for resources (lowercase)')
param baseName string = 'expensemgmt'

@description('Principal ID of the managed identity (passed from app-service module)')
param managedIdentityPrincipalId string

@description('Whether to deploy GenAI resources')
param deployGenAI bool = true

var uniqueSuffix = uniqueString(resourceGroup().id)
// All names must be lowercase to avoid Azure OpenAI validation errors
var openAIName = 'oai-${baseName}-${uniqueSuffix}'
var searchName = 'srch-${baseName}-${uniqueSuffix}'
// Azure OpenAI MUST be in swedencentral for GPT-4o
var openAILocation = 'swedencentral'

// Azure OpenAI - S0 SKU, swedencentral region
resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = if (deployGenAI) {
  name: openAIName
  location: openAILocation
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
}

// GPT-4o model deployment
resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = if (deployGenAI) {
  parent: openAI
  name: 'gpt-4o'
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-11-20'
    }
    raiPolicyName: 'Microsoft.Default'
  }
}

// AI Search - S0 SKU
resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' = if (deployGenAI) {
  name: searchName
  location: resourceGroup().location
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    publicNetworkAccess: 'enabled'
  }
}

// Role assignment: Cognitive Services OpenAI User for the managed identity on OpenAI
var cognitiveServicesOpenAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (deployGenAI) {
  name: guid(openAI.id, managedIdentityPrincipalId, cognitiveServicesOpenAIUserRoleId)
  scope: openAI
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIUserRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Role assignment: Search Index Data Contributor for the managed identity on AI Search
var searchIndexDataContributorRoleId = '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (deployGenAI) {
  name: guid(aiSearch.id, managedIdentityPrincipalId, searchIndexDataContributorRoleId)
  scope: aiSearch
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataContributorRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Outputs - use null-safe operators for conditional resources
output openAIEndpoint string = deployGenAI ? openAI.properties.endpoint : ''
output openAIModelName string = deployGenAI ? 'gpt-4o' : ''
output openAIName string = deployGenAI ? openAI.name : ''
output searchEndpoint string = deployGenAI ? 'https://${aiSearch.name}.search.windows.net' : ''
output searchName string = deployGenAI ? aiSearch.name : ''
