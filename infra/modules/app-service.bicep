param location string
param tags object
param appServiceName string
param appServicePlanId string
param keyVaultName string
param appInsightsConnectionString string
param clerkAuthority string
param frontendUrl string
param postmarkFromAddress string

var keyVaultReference = '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName='

resource appService 'Microsoft.Web/sites@2026-08-01' = {
  name: appServiceName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/health'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ASPNETCORE_HTTP_PORTS'
          value: '8080'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'KeyVault__Enabled'
          value: 'true'
        }
        {
          name: 'KeyVault__VaultUri'
          value: 'https://${keyVaultName}${environment().suffixes.keyvaultDns}/'
        }
        {
          name: 'ConnectionStrings__DevLogDbRuntime'
          value: '${keyVaultReference}connectionstrings-devlogdbruntime)'
        }
        {
          name: 'Clerk__Authority'
          value: clerkAuthority
        }
        {
          name: 'Clerk__AuthorizedParty'
          value: frontendUrl
        }
        {
          name: 'Clerk__SecretKey'
          value: '${keyVaultReference}clerk-secret-key)'
        }
        {
          name: 'OpenAi__ApiKey'
          value: '${keyVaultReference}openai-api-key)'
        }
        {
          name: 'OpenAi__EmbeddingModel'
          value: 'text-embedding-3-small'
        }
        {
          name: 'OpenAi__GenerationModel'
          value: 'gpt-4o-mini'
        }
        {
          name: 'OpenAi__MaxTokens'
          value: '2048'
        }
        {
          name: 'Anthropic__ApiKey'
          value: '${keyVaultReference}anthropic-api-key)'
        }
        {
          name: 'Anthropic__Model'
          value: 'claude-haiku-4-5'
        }
        {
          name: 'Anthropic__MaxTokens'
          value: '2048'
        }
        {
          name: 'Ai__GenerationProvider'
          value: 'OpenAI'
        }
        {
          name: 'Postmark__ApiKey'
          value: '${keyVaultReference}postmark-api-key)'
        }
        {
          name: 'Postmark__FromAddress'
          value: postmarkFromAddress
        }
        {
          name: 'Frontend__BaseUrl'
          value: frontendUrl
        }
        {
          name: 'Cors__FrontendOrigins__0'
          value: frontendUrl
        }
        {
          name: 'Storage__Provider'
          value: 'AzureBlob'
        }
        {
          name: 'Storage__AzureBlobConnectionString'
          value: '${keyVaultReference}storage-azureblobconnectionstring)'
        }
        {
          name: 'Storage__AzureBlobContainerName'
          value: 'cv-private'
        }
        {
          name: 'Storage__SignedUrlLifetimeMinutes'
          value: '10'
        }
        {
          name: 'MalwareScanning__Provider'
          value: 'AzureDefenderStorage'
        }
        {
          name: 'AiQuotas__Enabled'
          value: 'true'
        }
        {
          name: 'AiQuotas__MaxProviderCallsPerMinute'
          value: '20'
        }
        {
          name: 'AiQuotas__MaxProviderCallsPerDay'
          value: '100'
        }
        {
          name: 'AiQuotas__MaxProviderCallsPerMonth'
          value: '1000'
        }
        {
          name: 'AiCostControl__Enabled'
          value: 'true'
        }
        {
          name: 'AiCostControl__KillSwitchEnabled'
          value: 'false'
        }
        {
          name: 'AiCostControl__GlobalExpensiveRequestsPerMinute'
          value: '120'
        }
        {
          name: 'AiCostControl__AskAiPerMinutePerUser'
          value: '10'
        }
        {
          name: 'AiCostControl__AskAiPerMinutePerIp'
          value: '5'
        }
        {
          name: 'AiCostControl__ResumeImportPerHourPerUser'
          value: '5'
        }
        {
          name: 'AiCostControl__ResumeImportPerHourPerIp'
          value: '3'
        }
        {
          name: 'AiCostControl__SanitizePerHourPerUser'
          value: '10'
        }
        {
          name: 'AiCostControl__SanitizePerHourPerIp'
          value: '5'
        }
        {
          name: 'IpHashing__Secret'
          value: '${keyVaultReference}ip-hashing-secret)'
        }
        {
          name: 'PrivateBeta__Enabled'
          value: 'false'
        }
      ]
    }
  }
}

resource scmAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: appService
  name: 'scm'
  properties: {
    allow: true
  }
}

resource ftpAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: appService
  name: 'ftp'
  properties: {
    allow: false
  }
}

output appServiceId string = appService.id
output principalId string = appService.identity.principalId
output defaultHostname string = appService.properties.defaultHostName
