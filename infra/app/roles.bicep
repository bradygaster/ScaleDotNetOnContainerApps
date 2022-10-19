param principalId string
param principalType string = 'ServicePrincipal'
param resourceGroupName string = resourceGroup().name

// Role GUIDs are defined by Azure.  You can find them all here: https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles
var roles = [
  {
    name: 'blob_contrib-${principalId}'
    id: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  }
  {
    name: 'keyvault-crypto-service-encryption-user-${principalId}'
    id: 'e147488a-f6f5-4113-8e2d-b22465e65bf6'
  }
]

module roleAssignments '../core/security/role-assignments.bicep' = {
  name: 'role_assignments-${principalId}'
  params: {
    principalId: principalId
    principalType: principalType
    resourceGroupName: resourceGroupName
    roles: roles
  }
}
