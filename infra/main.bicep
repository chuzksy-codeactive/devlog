targetScope = 'subscription'

@minLength(1)
@maxLength(64)
param environmentName string

@minLength(1)
param location string

param sessionId string

param deployedBy string

param createdAt string

@description('Object ID of the principal deploying the template. The deploy phase supplies this value.')
param deployerObjectId string = ''

@description('PostgreSQL administrator login (management only, not the runtime app identity).')
param postgresAdministratorLogin string = 'devlog_admin'

@secure()
@description('PostgreSQL administrator password. Supply this at deploy time; do not commit it to parameters.json.')
param postgresAdministratorPassword string

@description('Least-privilege PostgreSQL runtime username expected by the application connection string secret.')
param postgresRuntimeUsername string = 'devlog_app'

@secure()
@description('Least-privilege PostgreSQL runtime user password used in the application connection string secret.')
param postgresRuntimePassword string

@description('Clerk issuer/authority URL for the production Clerk instance.')
param clerkAuthority string = 'https://REPLACE-WITH-YOUR-CLERK-FRONTEND-API-URL'

@description('Verified Postmark sender signature used by the API.')
param postmarkFromAddress string = 'REPLACE-WITH-A-VERIFIED-POSTMARK-SENDER-SIGNATURE'

@secure()
@description('Clerk secret key value to seed in Key Vault.')
param clerkSecretKey string

@secure()
@description('OpenAI API key value to seed in Key Vault.')
param openAiApiKey string

@secure()
@description('Anthropic API key value to seed in Key Vault.')
param anthropicApiKey string

@secure()
@description('Postmark API key value to seed in Key Vault.')
param postmarkApiKey string

@secure()
@description('IP hashing secret value (minimum 32 chars) to seed in Key Vault.')
param ipHashingSecret string

var tags = {
  'app-onboard-skill': 'true'
  'app-onboard-session-id': sessionId
  'created-at': createdAt
  environment: environmentName
  'deployed-by': deployedBy
}

// These names are the precomputed names from prepare-plan.json. Do not derive them
// with take(), substring(), uniqueString(), or other string manipulation.
var resourceGroupName = 'rg-devlog-prod-ca3c'
var staticWebAppName = 'swa-devlog-prod-ca3c'
var appServicePlanName = 'asp-devlog-prod-ca3c'
var appServiceName = 'app-devlog-api-prod-ca3c'
var postgresName = 'pg-devlog-prod-ca3c'
var storageAccountName = 'stdevlogprodca3c'
var keyVaultName = 'kv-devlog-prod-ca3c'
var logAnalyticsName = 'log-devlog-prod-ca3c'
var appInsightsName = 'appi-devlog-prod-ca3c'

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module logAnalytics './modules/log-analytics.bicep' = {
  name: 'log-analytics'
  scope: rg
  params: {
    location: location
    tags: tags
    workspaceName: logAnalyticsName
  }
}

module appInsights './modules/application-insights.bicep' = {
  name: 'application-insights'
  scope: rg
  params: {
    location: location
    tags: tags
    componentName: appInsightsName
    workspaceResourceId: logAnalytics.outputs.workspaceId
  }
}

module keyVault './modules/key-vault.bicep' = {
  name: 'key-vault'
  scope: rg
  params: {
    location: location
    tags: tags
    vaultName: keyVaultName
  }
}

module storage './modules/storage-account.bicep' = {
  name: 'storage-account'
  scope: rg
  params: {
    location: location
    tags: tags
    storageAccountName: storageAccountName
    containerName: 'cv-private'
  }
}

module postgres './modules/postgres.bicep' = {
  name: 'postgres'
  scope: rg
  params: {
    location: location
    tags: tags
    postgresName: postgresName
    administratorLogin: postgresAdministratorLogin
    administratorLoginPassword: postgresAdministratorPassword
  }
}

module keyVaultSecrets './modules/key-vault-secrets.bicep' = {
  name: 'key-vault-secrets'
  scope: rg
  params: {
    vaultName: keyVault.outputs.name
    postgresFqdn: postgres.outputs.fullyQualifiedDomainName
    postgresRuntimeUsername: postgresRuntimeUsername
    postgresRuntimePassword: postgresRuntimePassword
    clerkSecretKey: clerkSecretKey
    openAiApiKey: openAiApiKey
    anthropicApiKey: anthropicApiKey
    postmarkApiKey: postmarkApiKey
    storageAccountName: storage.outputs.name
    ipHashingSecret: ipHashingSecret
  }
}

module appServicePlan './modules/app-service-plan.bicep' = {
  name: 'app-service-plan'
  scope: rg
  params: {
    location: location
    tags: tags
    planName: appServicePlanName
  }
}

module staticWebApp './modules/static-web-app.bicep' = {
  name: 'static-web-app'
  scope: rg
  params: {
    location: location
    tags: tags
    staticWebAppName: staticWebAppName
    apiBaseUrl: 'https://${appServiceName}.azurewebsites.net'
  }
}

module api './modules/app-service.bicep' = {
  name: 'app-service'
  scope: rg
  params: {
    location: location
    tags: tags
    appServiceName: appServiceName
    appServicePlanId: appServicePlan.outputs.resourceId
    keyVaultName: keyVault.outputs.name
    appInsightsConnectionString: appInsights.outputs.connectionString
    clerkAuthority: clerkAuthority
    frontendUrl: 'https://${staticWebApp.outputs.defaultHostname}'
    postmarkFromAddress: postmarkFromAddress
  }
  dependsOn: [
    keyVaultSecrets
  ]
}

module roleAssignments './modules/role-assignments.bicep' = {
  name: 'role-assignments'
  scope: rg
  params: {
    keyVaultName: keyVault.outputs.name
    storageAccountName: storage.outputs.name
    deployerObjectId: deployerObjectId
    appPrincipalId: api.outputs.principalId
  }
}
