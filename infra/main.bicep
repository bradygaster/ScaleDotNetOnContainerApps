targetScope = 'subscription'

@minLength(1)
@maxLength(50)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environment string

@minLength(1)
@description('Primary location for all resources')
param location string

@minLength(1)
@description('Principal ID to use to deploy the app')
param principal string

var resourceToken = toLower(uniqueString(subscription().id, environment, location))
var tags = { 'azd-env-name': environment }
var abbrs = loadJsonContent('abbreviations.json')

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
    name: '${abbrs.resourcesResourceGroups}${environment}'
    location: location
    tags: tags
}

module resources 'resources.bicep' = {
    name: 'resources'
    scope: rg
    params: {
        location: location
        resourceToken: resourceToken
        tags: tags
    }
}

output AZURE_LOCATION string = location
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
