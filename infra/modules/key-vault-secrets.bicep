param vaultName string
param postgresFqdn string
param postgresRuntimeUsername string

@secure()
param postgresRuntimePassword string

@secure()
param clerkSecretKey string

@secure()
param openAiApiKey string

@secure()
param anthropicApiKey string

@secure()
param postmarkApiKey string

param storageAccountName string

@secure()
param ipHashingSecret string

resource vault 'Microsoft.KeyVault/vaults@2026-05-15' existing = {
  name: vaultName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2026-09-01' existing = {
  name: storageAccountName
}

var storageAzureBlobConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

resource runtimeDbConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2026-05-15' = {
  parent: vault
  name: 'connectionstrings-devlogdbruntime'
  properties: {
    value: 'Host=${postgresFqdn};Port=5432;Database=devlog;Username=${postgresRuntimeUsername};Password=${postgresRuntimePassword};Ssl Mode=Require;Trust Server Certificate=false;Timeout=15;Command Timeout=30'
  }
}

resource clerkSecret 'Microsoft.KeyVault/vaults/secrets@2026-05-15' = {
  parent: vault
  name: 'clerk-secret-key'
  properties: {
    value: clerkSecretKey
  }
}

resource openAiSecret 'Microsoft.KeyVault/vaults/secrets@2026-05-15' = {
  parent: vault
  name: 'openai-api-key'
  properties: {
    value: openAiApiKey
  }
}

resource anthropicSecret 'Microsoft.KeyVault/vaults/secrets@2026-05-15' = {
  parent: vault
  name: 'anthropic-api-key'
  properties: {
    value: anthropicApiKey
  }
}

resource postmarkSecret 'Microsoft.KeyVault/vaults/secrets@2026-05-15' = {
  parent: vault
  name: 'postmark-api-key'
  properties: {
    value: postmarkApiKey
  }
}

resource storageConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2026-05-15' = {
  parent: vault
  name: 'storage-azureblobconnectionstring'
  properties: {
    value: storageAzureBlobConnectionString
  }
}

resource ipHashingSecretResource 'Microsoft.KeyVault/vaults/secrets@2026-05-15' = {
  parent: vault
  name: 'ip-hashing-secret'
  properties: {
    value: ipHashingSecret
  }
}
