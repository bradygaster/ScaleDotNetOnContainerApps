param environmentName string
param location string = resourceGroup().location
param principalId string = ''

// Container apps host (including container registry)
module containerApps './core/host/container-apps.bicep' = {
  name: 'container-apps'
  params: {
    environmentName: environmentName
    location: location
    logAnalyticsWorkspaceName: monitoring.outputs.logAnalyticsWorkspaceName
    containerRegistrySku: { name: 'Basic' }
  }
}

// Backing storage for Azure functions backend API
module storage './core/storage/storage-account.bicep' = {
  name: 'storage'
  params: {
    environmentName: environmentName
    location: location
    allowBlobPublicAccess: true
    managedIdentity: false
  }
}

module storageContainer './core/storage/storage-container.bicep' = {
  name: 'storagecontainer'
  params: {
    environmentName: environmentName
    location: location
    storageName: storage.outputs.name
    blobServicesName: 'default'
    containerName: 'keys'
  }
}

// Backing storage for Azure functions backend API
module signalR './core/messaging/signalr.bicep' = {
  name: 'signalr'
  params: {
    environmentName: environmentName
    location: location
  }
}

// Store secrets in a keyvault
module keyVault './core/security/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    environmentName: environmentName
    location: location
    principalId: principalId
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

module keyVaultKey './core/security/keyvault-key.bicep' = {
  name: 'keyvaultkey'
  params: {
    environmentName: environmentName
    location: location
    keyVaultName: keyVault.outputs.keyVaultName
    keyName: 'razorkey'
  }
}

// Monitor application with Azure Monitor
module monitoring './core/monitor/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    environmentName: environmentName
    location: location
  }
}

// Assign appropriate roles to the local user
module roleAssignments './app/roles.bicep' = {
  name: 'role-assignments'
  params: {
    principalId: principalId
    principalType: 'User'
  }
}

output APPLICATIONINSIGHTS_CONNECTION_STRING string = monitoring.outputs.applicationInsightsConnectionString
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerApps.outputs.containerRegistryEndpoint
output AZURE_CONTAINER_REGISTRY_NAME string = containerApps.outputs.containerRegistryName
output AZURE_KEY_VAULT_ENDPOINT string = keyVault.outputs.keyVaultEndpoint
