param environment string
param image string
param location string
param principal string

var resourceToken = toLower(uniqueString(subscription().id, environment, location))
var tags = { 'azd-env-name': environment }
var abbrs = loadJsonContent('abbreviations.json')

resource acr 'Microsoft.ContainerRegistry/registries@2022-02-01-preview' existing = {
  name: '${abbrs.containerRegistryRegistries}${resourceToken}'
}

resource env 'Microsoft.App/managedEnvironments@2022-03-01' existing = {
  name: '${abbrs.appManagedEnvironments}${resourceToken}'
}

resource storage 'Microsoft.Storage/storageAccounts@2021-09-01' existing = {
  name: '${abbrs.storageStorageAccounts}${resourceToken}'
}

resource signalr 'Microsoft.SignalRService/signalR@2022-02-01' existing = {
  name: '${abbrs.signalRServiceSignalR}${resourceToken}'
}

resource containerapp 'Microsoft.App/containerApps@2022-03-01' = {
  name: '${environment}blazorserverapp'
  location: location
  tags: union(tags, { 'azd-service-name': '${environment}blazorserverapp' })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      activeRevisionsMode: 'single'
      secrets: [
        {
          name: 'container-registry-password'
          value: acr.listCredentials().passwords[0].value
        }
      ]
      registries: [
        {
          server: '${acr.name}.azurecr.io'
          username: acr.name
          passwordSecretRef: 'container-registry-password'
        }
      ]
      ingress: { 
        external: true
        targetPort: 80
      }
    }
    template: {
      containers: [
        {
          image: image
          name: '${environment}blazorserverapp'
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
              value: signalr.listKeys().primaryConnectionString
            }
            {
              name: 'BlobStorageUri'
              value: 'https://${storage.name}.blob.core.windows.net/keys/keys.xml'
            }
            {
              name: 'KeyVaultURI'
              value: 'https://${abbrs.keyVaultVaults}${resourceToken}.vault.azure.net/keys/key${resourceToken}/'
            }
          ] 
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2019-09-01' = {
  name: '${abbrs.keyVaultVaults}${resourceToken}'
  location: location
  properties: {
    enabledForDeployment: true
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: true
    enableRbacAuthorization: true
    tenantId: tenant().tenantId
    accessPolicies: [
      {
        tenantId: tenant().tenantId
        objectId: containerapp.identity.principalId
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
      {
        tenantId: tenant().tenantId
        objectId: principal
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
    ]
    sku: {
      name: 'standard'
      family: 'A'
    }
  }
}

resource key 'Microsoft.KeyVault/vaults/keys@2022-07-01' = {
  name: 'key${resourceToken}'
  parent: keyVault
  properties: {
    kty: 'EC'
    keySize: 2048
    curveName: 'P-256'
    keyOps: [
      'decrypt'
      'encrypt'
      'import'
      'release'
      'sign'
      'unwrapKey'
      'verify'
      'wrapKey'
      'decrypt'
    ]
  }
}
