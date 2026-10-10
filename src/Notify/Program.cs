var builder = WebApplication.CreateBuilder(args);

// Register feature modules and configuration bindings
builder.Services.ConfigureApplicationInbox(builder.Configuration);
builder.Services.ConfigureSmsFeature(builder.Configuration);
builder.Services.ConfigureEmailFeature(builder.Configuration);
builder.Services.Configure<AppSettings>(builder.Configuration);

// Register messaging, mediator, and Carter endpoint modules
builder.Services.ConfigureMediatR();
builder.Services.ConfigureBroker(builder.Configuration);
builder.Services.AddCarter();

// Register Health Checks for container health probes and readiness monitoring
builder.Services.AddHealthChecks();

// Swagger API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Enable Swagger UI in development environment
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map Health Check endpoint at /health
app.MapHealthChecks("/health");

// Map all Carter endpoint modules (REST APIs)
app.MapCarter();

app.Run();
