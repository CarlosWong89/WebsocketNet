using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using WebsocketNet_JwtAuth.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

//builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();

const string SecretKey = "7A7E8F2A3C6D9B1E5F4A8C2D7E1B9F3A";

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p =>
    {
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            IssuerSigningKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(SecretKey))
        };

        // Read token from query string for websocket
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["token"];

                if (!string.IsNullOrEmpty(token) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/ws"))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });


builder.Services.AddAuthorization();

var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.MapOpenApi();
//}

//app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.UseWebSockets();


// LOGIN API
app.MapPost("/login", (LoginModel login) =>
{
    // demo user
    if (login.Username != "admin" ||
        login.Password != "123456")
    {
        return Results.Unauthorized();
    }

    var claims = new[]
    {
        new Claim(ClaimTypes.Name, login.Username)
    };

    var key = new SymmetricSecurityKey(
        Encoding.UTF8.GetBytes(SecretKey));

    var creds = new SigningCredentials(
        key,
        SecurityAlgorithms.HmacSha256);

    var jwt = new JwtSecurityToken(
        claims: claims,
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials: creds);

    var token = new JwtSecurityTokenHandler()
        .WriteToken(jwt);

    return Results.Ok(new
    {
        token
    });
});


// WEBSOCKET
app.Map("/ws", async context =>
{
    if (!context.User.Identity!.IsAuthenticated)
    {
        context.Response.StatusCode = 401;
        return;
    }

    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket =
        await context.WebSockets.AcceptWebSocketAsync();

    string username = context.User.Identity!.Name!;

    Console.WriteLine($"{username} connected");

    byte[] buffer = new byte[4096];

    while (socket.State == WebSocketState.Open)
    {
        var result =
            await socket.ReceiveAsync(
                buffer,
                CancellationToken.None);

        if (result.MessageType ==
            WebSocketMessageType.Close)
        {
            await socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "",
                CancellationToken.None);

            break;
        }

        var message =
            Encoding.UTF8.GetString(
                buffer,
                0,
                result.Count);

        Console.WriteLine($"{username}: {message}");

        var reply =
            Encoding.UTF8.GetBytes(
                $"Server reply: {message}");

        await socket.SendAsync(
            reply,
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }
}).RequireAuthorization();

//app.MapControllers();

app.Run();
