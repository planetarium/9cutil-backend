using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Mimir.Worker;
using Mimir.Worker.Services;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

string configPath = Environment.GetEnvironmentVariable("STORE_CONFIG_FILE") ?? "appsettings.json";
builder.Configuration
    .AddJsonFile(configPath, optional: true, reloadOnChange: true)
    .AddEnvironmentVariables("STORE_");

builder.Services.Configure<Configuration>(builder.Configuration.GetSection("Configuration"));

builder.Services.AddSingleton<IStateService, HeadlessStateService>();
builder.Services.AddHeadlessGQLClient()
    .ConfigureHttpClient((provider, client) =>
    {
        var headlessStateServiceOption = provider.GetRequiredService<IOptions<Configuration>>();
        client.BaseAddress = headlessStateServiceOption.Value.HeadlessEndpoint;

        if (headlessStateServiceOption.Value.JwtSecretKey is not null && headlessStateServiceOption.Value.JwtIssuer is not null)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(headlessStateServiceOption.Value.JwtSecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: headlessStateServiceOption.Value.JwtIssuer,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: creds);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
    });

builder.Services.AddSingleton(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<IOptions<Configuration>>().Value;
    var logger = serviceProvider.GetRequiredService<ILogger<MongoDbWorker>>();
    return new MongoDbWorker(logger, config.MongoDbConnectionString, config.DatabaseName);
});
builder.Services.AddHostedService<Initializer>();

var host = builder.Build();
host.Run();
