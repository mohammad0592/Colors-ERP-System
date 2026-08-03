using Colors.Api.Extensions;
using Colors.Infrastructure;
using Colors.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Database, Identity and everything else Infrastructure owns.
builder.Services.AddInfrastructure(builder.Configuration);

// Reading and checking the access token on every request.
builder.Services.AddJwtAuthentication(builder.Configuration);

// Lets the React development server reach the API. Not needed in production,
// where both are served from the same address.
builder.Services.AddFrontendCors(builder.Configuration);

var app = builder.Build();

// Roles and the first administrator. Safe to run every start — it only adds what
// is missing. Database migrations are NOT run here: they are a deliberate step in
// the deployment, taken after a backup (specification section 15).
await IdentitySeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(CorsExtensions.PolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
