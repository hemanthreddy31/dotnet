var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.dotnet10>("dotnet10");

builder.Build().Run();
