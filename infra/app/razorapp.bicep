param environmentName string
param location string = resourceGroup().location

param applicationInsightsName string = ''
param containerAppsEnvironmentName string = ''
param containerRegistryName string = ''
param imageName string = ''
param keyVaultName string = ''
param serviceName string = 'razorapp'

var abbrs = loadJsonContent('../abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

module razorapp '../core/host/container-app.bicep' = {
  name: '${serviceName}-container-app'
  params: {
    environmentName: environmentName
    location: location
    name: '${environmentName}razorapp'
    containerAppsEnvironmentName: containerAppsEnvironmentName
    containerRegistryName: containerRegistryName
    containerCpuCoreCount: '1.0'
    containerMemory: '2.0Gi'
    env: [
      {
        name: 'ASPNETCORE_ENVIRONMENT'
        value: 'Development'
      }
      {
        name: 'ASPNETCORE_LOGGING__CONSOLE__DISABLECOLORS'
        value: 'true'
      }
      {
        name: 'AZURE_KEY_VAULT_ENDPOINT'
        value: keyVault.properties.vaultUri
      }
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: applicationInsights.properties.ConnectionString
      }
    ]
    imageName: !empty(imageName) ? imageName : 'nginx:latest'
    keyVaultName: keyVault.name
    serviceName: serviceName
    external: true
    targetPort: 80
  }
}

module keyVaultAccess '../core/security/keyvault-access.bicep' = {
  name: '${serviceName}-keyvault-access'
  params: {
    environmentName: environmentName
    location: location
    principalId: razorapp.outputs.identityPrincipalId
    keyVaultName: keyVault.name
    permissions: {
      keys: [
        'all'
      ]
      secrets: [
        'all'
      ]
      certificates: [
        'all'
      ]
    }
  }
}


// Assign appropriate roles to the local user
module roleAssignments '../app/roles.bicep' = {
  name: 'role-assignments'
  params: {
    principalId: razorapp.outputs.identityPrincipalId
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: !empty(applicationInsightsName) ? applicationInsightsName : '${abbrs.insightsComponents}${resourceToken}'
}

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: !empty(keyVaultName) ? keyVaultName : '${abbrs.keyVaultVaults}${resourceToken}'
}

output RAZORAPP_IDENTITY_PRINCIPAL_ID string = razorapp.outputs.identityPrincipalId
output RAZORAPP_NAME string = razorapp.outputs.name
output RAZORAPP_URI string = razorapp.outputs.uri
