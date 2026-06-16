
using System.Net.WebSockets;
using System.Text;

public class Program
{
    private static string? _nodeName ;

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

        app.UseWebSockets();

        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            Console.WriteLine($"[{_nodeName}] Client connected.");

            using var socket = await context.WebSockets.AcceptWebSocketAsync();

            await HandleSocketAsync(socket);

            Console.WriteLine($"[{_nodeName}] Client disconnected.");
        });

        app.Run();
    }

    private static async Task HandleSocketAsync(WebSocket socket)
    {
        var buffer = new byte[4 * 1024];

        while (socket.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();

            WebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None);

                // Client requested to close connection
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed",
                        CancellationToken.None);

                    return;
                }

                ms.Write(buffer, 0, result.Count);

            } while (!result.EndOfMessage);

            // Convert complete message to string
            string message = Encoding.UTF8.GetString(ms.ToArray());

            Console.WriteLine($"[{_nodeName}] Received: {message}");

            // Send response back
            string responseText = $"[{_nodeName} client] Received: {message}";

            byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);

            await socket.SendAsync(
                new ArraySegment<byte>(responseBytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            Console.WriteLine($"[{_nodeName}] Sent: {message}");
        }
    }
}
