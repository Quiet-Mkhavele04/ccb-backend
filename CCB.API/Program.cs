using System.Text;
using CCB.API.Data;
using CCB.API.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using CCB.API.Services.Payments;

var builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddScoped<
    IPaymentProvider,
    DemoPaymentProvider
>();

// Register SignalR.
builder.Services.AddSignalR();

builder.Services.AddDbContext<CCBDbContext>(
    options =>
        options.UseSqlServer(
            builder.Configuration
                .GetConnectionString(
                    "CCBConnection"
                )
        )
);

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "ExpoApp",
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    );
});

var jwtKey =
    builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "The Jwt:Key setting is missing from appsettings.json."
    );
}

builder.Services
    .AddAuthentication(
        JwtBearerDefaults
            .AuthenticationScheme
    )
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,

                ValidateIssuerSigningKey =
                    true,

                ValidIssuer =
                    builder.Configuration[
                        "Jwt:Issuer"
                    ],

                ValidAudience =
                    builder.Configuration[
                        "Jwt:Audience"
                    ],

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtKey
                        )
                    ),

                ClockSkew = TimeSpan.Zero
            };

        /*
         * SignalR may send the JWT token through
         * the access_token query parameter when
         * it uses a WebSocket connection.
         */
        options.Events =
            new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken =
                        context.Request.Query[
                            "access_token"
                        ];

                    var requestPath =
                        context.HttpContext
                            .Request.Path;

                    if (
                        !string.IsNullOrWhiteSpace(
                            accessToken
                        ) &&
                        requestPath.StartsWithSegments(
                            "/hubs/chat"
                        )
                    )
                    {
                        context.Token =
                            accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
    });

builder.Services.AddAuthorization();

builder.Services
    .AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(
    options =>
    {
        options.AddSecurityDefinition(
            "Bearer",
            new OpenApiSecurityScheme
            {
                Name =
                    "Authorization",

                Type =
                    SecuritySchemeType.Http,

                Scheme =
                    "bearer",

                BearerFormat =
                    "JWT",

                In =
                    ParameterLocation.Header,

                Description =
                    "Paste your JWT access token here."
            }
        );

        options.AddSecurityRequirement(
            document =>
                new OpenApiSecurityRequirement
                {
                    [
                        new
                            OpenApiSecuritySchemeReference(
                                "Bearer",
                                document
                            )
                    ] = []
                }
        );
    }
);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("ExpoApp");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet(
    "/api/health",
    () =>
    {
        return Results.Ok(new
        {
            message =
                "CCB API is working",

            serverTime =
                DateTime.UtcNow
        });
    }
);

// SignalR real-time messaging endpoint.
app.MapHub<ChatHub>("/hubs/chat");

app.MapControllers();

app.Run();