param location string
param environmentName string
param managedEnvironmentName string
param workerAppName string
param workerImage string
param claimsContainerName string
param blobEndpoint string
param serviceBusNamespace string
param serviceBusTopic string
param keyVaultUri string
param applicationInsightsConnectionString string
param tags object = {}

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: managedEnvironmentName
  location: location
  tags: tags
  properties: {
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

resource workerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: workerAppName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 8080
        transport: 'auto'
      }
    }
    template: {
      containers: [
        {
          name: 'claims-worker'
          image: workerImage
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environmentName
            }
            {
              name: 'ClaimsStorage__ContainerName'
              value: claimsContainerName
            }
            {
              name: 'ClaimsStorage__BlobEndpoint'
              value: blobEndpoint
            }
            {
              name: 'ServiceBus__Namespace'
              value: serviceBusNamespace
            }
            {
              name: 'ServiceBus__TopicName'
              value: serviceBusTopic
            }
            {
              name: 'KeyVault__Uri'
              value: keyVaultUri
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: applicationInsightsConnectionString
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 2
      }
    }
  }
}

output workerAppName string = workerApp.name
output managedEnvironmentName string = managedEnvironment.name
