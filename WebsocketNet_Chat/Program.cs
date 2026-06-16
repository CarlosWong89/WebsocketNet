var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

//builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.MapOpenApi();
//}

//app.UseHttpsRedirection();

//app.UseAuthorization();

//app.MapControllers();

app.UseWebSockets();


var channels = new Dictionary<string, HashSet<System.Net.WebSockets.WebSocket>>();
var lockObj = new object();

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    var buffer = new byte[1024 * 4];

    string currentChannel = null;

    while (socket.State == System.Net.WebSockets.WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

        if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
            break;

        var msg = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);

        var json = System.Text.Json.JsonDocument.Parse(msg);
        var type = json.RootElement.GetProperty("type").GetString();

        if (type == "join")
        {
            currentChannel = json.RootElement.GetProperty("channel").GetString();

            lock (lockObj)
            {
                if (!channels.ContainsKey(currentChannel))
                    channels[currentChannel] = new HashSet<System.Net.WebSockets.WebSocket>();

                channels[currentChannel].Add(socket);
            }
        }
        else if (type == "message")
        {
            var channel = json.RootElement.GetProperty("channel").GetString();
            var text = json.RootElement.GetProperty("text").GetString();

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                channel,
                text
            });

            var data = System.Text.Encoding.UTF8.GetBytes(payload);

            lock (lockObj)
            {
                if (channels.ContainsKey(channel))
                {
                    foreach (var client in channels[channel])
                    {
                        if (client.State == System.Net.WebSockets.WebSocketState.Open)
                        {
                            _ = client.SendAsync(
                                data,
                                System.Net.WebSockets.WebSocketMessageType.Text,
                                true,
                                CancellationToken.None
                            );
                        }
                    }
                }
            }
        }
    }

    if (currentChannel != null)
    {
        lock (lockObj)
        {
            if (channels.ContainsKey(currentChannel))
                channels[currentChannel].Remove(socket);
        }
    }

    await socket.CloseAsync(
        System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
        "closed",
        CancellationToken.None);
});


app.Run();
