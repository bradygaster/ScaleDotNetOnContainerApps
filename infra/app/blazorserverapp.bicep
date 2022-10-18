param environmentName string
param location string = resourceGroup().location

param applicationInsightsName string = ''
param containerAppsEnvironmentName string = ''
param containerRegistryName string = ''
param imageName string = ''
param keyVaultName string = ''
param serviceName string = 'blazorserverapp'
param signlaRName string = ''
param storageName string = ''

var abbrs = loadJsonContent('../abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

module blazorserverapp '../core/host/container-app.bicep' = {
  name: '${serviceName}-container-app'
  params: {
    environmentName: environmentName
    location: location
    name: '${environmentName}blazorserverapp'
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
        name: 'AzureSignalRConnectionString'
        value: signalR.listKeys().primaryConnectionString
      }
      {
        name: 'BlobStorageUri'
        value: 'https://${storage.name}.blob.${environment().suffixes.storage}/keys/keys.xml'
      }
      {
        name: 'KeyVaultURI'
        value: 'https://${abbrs.keyVaultVaults}${resourceToken}${environment().suffixes.keyvaultDns}/keys/key1/'
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
    principalId: blazorserverapp.outputs.identityPrincipalId
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

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: !empty(applicationInsightsName) ? applicationInsightsName : '${abbrs.insightsComponents}${resourceToken}'
}

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: !empty(keyVaultName) ? keyVaultName : '${abbrs.keyVaultVaults}${resourceToken}'
}

resource signalR 'Microsoft.SignalRService/signalR@2022-02-01' existing = {
  name: !empty(signlaRName) ? signlaRName : '${abbrs.signalRServiceSignalR}${resourceToken}'
}

resource storage 'Microsoft.Storage/storageAccounts@2021-09-01' existing = {
  name: !empty(storageName) ? storageName : '${abbrs.storageStorageAccounts}${resourceToken}'
}

output BLAZORSERVERAPP_IDENTITY_PRINCIPAL_ID string = blazorserverapp.outputs.identityPrincipalId
output BLAZORSERVERAPP_NAME string = blazorserverapp.outputs.name
output BLAZORSERVERAPP_URI string = blazorserverapp.outputs.uri
