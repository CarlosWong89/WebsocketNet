
using StackExchange.Redis;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

public class Program
{
    private static string? _nodeName;
    private static string? _channel;

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        //builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        //builder.Services.AddOpenApi();

        var app = builder.Build();

        //// Configure the HTTP request pipeline.
        //if (app.Environment.IsDevelopment())
        //{
        //    app.MapOpenApi();
        //}

        //app.UseHttpsRedirection();

        //app.UseAuthorization();

        //app.MapControllers();

        _nodeName = Environment.GetEnvironmentVariable("NODE_NAME") ?? "debug";
        _channel = _nodeName ?? "chat";

        // store active sockets
        var sockets = new List<WebSocket>();

        // Redis connection
        var redis = ConnectionMultiplexer.Connect("redis:6379"); // Docker setup
        // var redis = ConnectionMultiplexer.Connect("127.0.0.1:6379"); // Debug, local Redis
        var pub = redis.GetSubscriber();

        app.UseWebSockets();

        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();
            sockets.Add(socket);

            Console.WriteLine($"[{_nodeName}] Client connected");

            // Subscribe to Redis channel once per node
            await pub.SubscribeAsync(_channel, async (channel, message) =>
            {
                var msg = Encoding.UTF8.GetBytes($"[{_nodeName}] {message}");

                foreach (var s in sockets.ToArray())
                {
                    if (s.State == WebSocketState.Open)
                    {
                        await s.SendAsync(msg, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            });

            // Receive messages from client
            var buffer = new byte[1024 * 4];

            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    Console.WriteLine($"[{_nodeName}] received: {message}");

                    // publish to Redis → other nodes receive it
                    await pub.PublishAsync(_channel, message);
                }
            }
        });

        app.Run();
    }
}
