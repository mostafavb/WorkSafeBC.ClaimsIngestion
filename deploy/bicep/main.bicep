targetScope = 'resourceGroup'

@description('Environment short name such as dev, test, or prod.')
param environmentName string = 'dev'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short application prefix used to derive resource names.')
param prefix string = 'wsbcclaims'

@description('Container image for the worker container app.')
param workerImage string = 'ghcr.io/mostafavb/worksafebc-claims-ingestion-worker:dev'

@allowed([
  'rolling'
  'blueGreen'
  'canary'
])
@description('Deployment strategy for the worker runtime.')
param deploymentStrategy string = 'rolling'

@allowed([
  'blue'
  'green'
])
@description('Active slot for blue/green deployments.')
param activeSlot string = 'blue'

@description('Maximum replicas for the stable worker deployment.')
param stableMaxReplicas int = 2

@description('Maximum replicas for the canary worker deployment.')
param canaryMaxReplicas int = 1

@description('Claims blob container name.')
param claimsContainerName string = 'claims-inbox'

@description('Service Bus topic name for claim events.')
param claimTopicName string = 'claims-ingestion'

@description('Service Bus subscription name for worker consumers and replay tooling.')
param claimSubscriptionName string = 'claims-worker'

@description('SQL administrator username.')
param sqlAdministratorLogin string

@secure()
@description('SQL administrator password.')
param sqlAdministratorPassword string

var resourceToken = toLower(replace('${prefix}${environmentName}', '-', ''))
var useRollingDeployment = deploymentStrategy == 'rolling'
var useBlueGreenDeployment = deploymentStrategy == 'blueGreen'
var useCanaryDeployment = deploymentStrategy == 'canary'
var blueWorkerEnabled = useBlueGreenDeployment && activeSlot == 'blue'
var greenWorkerEnabled = useBlueGreenDeployment && activeSlot == 'green'
var rollingWorkerAppName = take('${resourceToken}worker', 32)
var blueWorkerAppName = take('${resourceToken}blueworker', 32)
var greenWorkerAppName = take('${resourceToken}greenworker', 32)
var stableWorkerAppName = take('${resourceToken}stableworker', 32)
var canaryWorkerAppName = take('${resourceToken}canaryworker', 32)
var tags = {
  environment: environmentName
  application: 'WorkSafeBC.ClaimsIngestion'
  managedBy: 'bicep'
}

module storage './modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    storageAccountName: take('${resourceToken}st', 24)
    containerName: claimsContainerName
    tags: tags
  }
}

module messaging './modules/messaging.bicep' = {
  name: 'messaging'
  params: {
    location: location
    namespaceName: take('${resourceToken}sb', 50)
    topicName: claimTopicName
    subscriptionName: claimSubscriptionName
    tags: tags
  }
}

module keyVault './modules/keyvault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    keyVaultName: take('${resourceToken}kv', 24)
    tags: tags
  }
}

module data './modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: location
    sqlServerName: take('${resourceToken}sql', 63)
    databaseName: 'ClaimsIngestion${toUpper(environmentName)}'
    administratorLogin: sqlAdministratorLogin
    administratorPassword: sqlAdministratorPassword
    tags: tags
  }
}

module observability './modules/observability.bicep' = {
  name: 'observability'
  params: {
    location: location
    workspaceName: take('${resourceToken}log', 63)
    appInsightsName: take('${resourceToken}appi', 260)
    tags: tags
  }
}

module rollingWorker './modules/worker-app.bicep' = if (useRollingDeployment) {
  name: 'compute-rolling'
  params: {
    location: location
    environmentName: environmentName
    managedEnvironmentName: take('${resourceToken}cae', 32)
    workerAppName: rollingWorkerAppName
    workerImage: workerImage
    claimsContainerName: storage.outputs.containerName
    blobEndpoint: storage.outputs.blobEndpoint
    serviceBusNamespace: messaging.outputs.namespaceName
    serviceBusTopic: messaging.outputs.topicName
    keyVaultUri: keyVault.outputs.vaultUri
    applicationInsightsConnectionString: observability.outputs.applicationInsightsConnectionString
    workerEnabled: true
    minReplicas: 1
    maxReplicas: stableMaxReplicas
    tags: tags
  }
}

module blueWorker './modules/worker-app.bicep' = if (useBlueGreenDeployment) {
  name: 'compute-blue'
  params: {
    location: location
    environmentName: environmentName
    managedEnvironmentName: take('${resourceToken}cae', 32)
    workerAppName: blueWorkerAppName
    workerImage: workerImage
    claimsContainerName: storage.outputs.containerName
    blobEndpoint: storage.outputs.blobEndpoint
    serviceBusNamespace: messaging.outputs.namespaceName
    serviceBusTopic: messaging.outputs.topicName
    keyVaultUri: keyVault.outputs.vaultUri
    applicationInsightsConnectionString: observability.outputs.applicationInsightsConnectionString
    workerEnabled: blueWorkerEnabled
    minReplicas: blueWorkerEnabled ? 1 : 0
    maxReplicas: blueWorkerEnabled ? stableMaxReplicas : 1
    tags: union(tags, {
      deploymentSlot: 'blue'
      deploymentStrategy: deploymentStrategy
    })
  }
}

module greenWorker './modules/worker-app.bicep' = if (useBlueGreenDeployment) {
  name: 'compute-green'
  params: {
    location: location
    environmentName: environmentName
    managedEnvironmentName: take('${resourceToken}cae', 32)
    workerAppName: greenWorkerAppName
    workerImage: workerImage
    claimsContainerName: storage.outputs.containerName
    blobEndpoint: storage.outputs.blobEndpoint
    serviceBusNamespace: messaging.outputs.namespaceName
    serviceBusTopic: messaging.outputs.topicName
    keyVaultUri: keyVault.outputs.vaultUri
    applicationInsightsConnectionString: observability.outputs.applicationInsightsConnectionString
    workerEnabled: greenWorkerEnabled
    minReplicas: greenWorkerEnabled ? 1 : 0
    maxReplicas: greenWorkerEnabled ? stableMaxReplicas : 1
    tags: union(tags, {
      deploymentSlot: 'green'
      deploymentStrategy: deploymentStrategy
    })
  }
}

module stableWorker './modules/worker-app.bicep' = if (useCanaryDeployment) {
  name: 'compute-stable'
  params: {
    location: location
    environmentName: environmentName
    managedEnvironmentName: take('${resourceToken}cae', 32)
    workerAppName: stableWorkerAppName
    workerImage: workerImage
    claimsContainerName: storage.outputs.containerName
    blobEndpoint: storage.outputs.blobEndpoint
    serviceBusNamespace: messaging.outputs.namespaceName
    serviceBusTopic: messaging.outputs.topicName
    keyVaultUri: keyVault.outputs.vaultUri
    applicationInsightsConnectionString: observability.outputs.applicationInsightsConnectionString
    workerEnabled: true
    minReplicas: 1
    maxReplicas: stableMaxReplicas
    tags: union(tags, {
      deploymentSlot: 'stable'
      deploymentStrategy: deploymentStrategy
    })
  }
}

module canaryWorker './modules/worker-app.bicep' = if (useCanaryDeployment) {
  name: 'compute-canary'
  params: {
    location: location
    environmentName: environmentName
    managedEnvironmentName: take('${resourceToken}cae', 32)
    workerAppName: canaryWorkerAppName
    workerImage: workerImage
    claimsContainerName: storage.outputs.containerName
    blobEndpoint: storage.outputs.blobEndpoint
    serviceBusNamespace: messaging.outputs.namespaceName
    serviceBusTopic: messaging.outputs.topicName
    keyVaultUri: keyVault.outputs.vaultUri
    applicationInsightsConnectionString: observability.outputs.applicationInsightsConnectionString
    workerEnabled: true
    minReplicas: 1
    maxReplicas: canaryMaxReplicas
    tags: union(tags, {
      deploymentSlot: 'canary'
      deploymentStrategy: deploymentStrategy
    })
  }
}

output storageAccountName string = storage.outputs.storageAccountName
output blobContainerName string = storage.outputs.containerName
output serviceBusNamespace string = messaging.outputs.namespaceName
output serviceBusTopic string = messaging.outputs.topicName
output keyVaultUri string = keyVault.outputs.vaultUri
output sqlServerName string = data.outputs.sqlServerName
output sqlDatabaseName string = data.outputs.databaseName
output containerAppName string = useRollingDeployment ? rollingWorkerAppName : ''
output activeBlueGreenAppName string = useBlueGreenDeployment ? (activeSlot == 'blue' ? blueWorkerAppName : greenWorkerAppName) : ''
output inactiveBlueGreenAppName string = useBlueGreenDeployment ? (activeSlot == 'blue' ? greenWorkerAppName : blueWorkerAppName) : ''
output stableCanaryAppName string = useCanaryDeployment ? stableWorkerAppName : ''
output canaryAppName string = useCanaryDeployment ? canaryWorkerAppName : ''
output applicationInsightsConnectionString string = observability.outputs.applicationInsightsConnectionString
