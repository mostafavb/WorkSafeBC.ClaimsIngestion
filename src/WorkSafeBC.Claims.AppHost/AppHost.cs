var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.WorkSafeBC_Claims_Worker>("claims-worker")
    .WithEnvironment("ClaimsStorage__ContainerName", "claims-inbox")
    .WithEnvironment("ClaimsStorage__ConnectionString", "UseDevelopmentStorage=true")
    .WithEnvironment("RabbitMq__HostName", "localhost")
    .WithEnvironment("RabbitMq__Port", "5672")
    .WithEnvironment("RabbitMq__ExchangeName", "claims.ingestion")
    .WithEnvironment("RabbitMq__RoutingKey", "claims.ingested")
    .WithEnvironment("Worker__PollingIntervalSeconds", "15");

builder.Build().Run();
