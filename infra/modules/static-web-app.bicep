param location string
param tags object
param staticWebAppName string
param apiBaseUrl string

resource staticWebApp 'Microsoft.Web/staticSites@2025-05-01' = {
  name: staticWebAppName
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

resource appSettings 'Microsoft.Web/staticSites/config@2025-05-01' = {
  parent: staticWebApp
  name: 'appsettings'
  properties: {
    NEXT_PUBLIC_API_BASE_URL: apiBaseUrl
  }
}

output resourceId string = staticWebApp.id
output defaultHostname string = staticWebApp.properties.defaultHostname
