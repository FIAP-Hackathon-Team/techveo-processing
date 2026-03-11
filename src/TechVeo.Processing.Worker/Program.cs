using Microsoft.AspNetCore.Builder;
using TechVeo.Processing.Infra;

var builder = Host.CreateApplicationBuilder(args);
{
    builder.Services.AddWorker();
    builder.Services.AddInfra();
}

var app = builder.Build();
{
    app.Run();
}
