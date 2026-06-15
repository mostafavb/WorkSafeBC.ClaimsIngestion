targetScope = 'resourceGroup'

@description('Environment short name such as dev, test, or prod.')
param environmentName string = 'dev'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short application prefix used to derive resource names.')
param prefix string = 'wsbcclaims'

@description('Container image for the worker container app.')
param workerImage string = 'ghcr.io/mostafavb/worksafebc-claims-ingestion-worker:dev'

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

module compute './modules/worker-app.bicep' = {
  name: 'compute'
  params: {
    location: location
    environmentName: environmentName
    managedEnvironmentName: take('${resourceToken}cae', 32)
    workerAppName: take('${resourceToken}worker', 32)
    workerImage: workerImage
    claimsContainerName: storage.outputs.containerName
    blobEndpoint: storage.outputs.blobEndpoint
    serviceBusNamespace: messaging.outputs.namespaceName
    serviceBusTopic: messaging.outputs.topicName
    keyVaultUri: keyVault.outputs.vaultUri
    applicationInsightsConnectionString: observability.outputs.applicationInsightsConnectionString
    tags: tags
  }
}

output storageAccountName string = storage.outputs.storageAccountName
output blobContainerName string = storage.outputs.containerName
output serviceBusNamespace string = messaging.outputs.namespaceName
output serviceBusTopic string = messaging.outputs.topicName
output keyVaultUri string = keyVault.outputs.vaultUri
output sqlServerName string = data.outputs.sqlServerName
output sqlDatabaseName string = data.outputs.databaseName
output containerAppName string = compute.outputs.workerAppName
output applicationInsightsConnectionString string = observability.outputs.applicationInsightsConnectionString
