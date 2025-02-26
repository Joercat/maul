using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
var app = builder.Build();

app.UseStaticFiles();
app.MapHub<GameHub>("/gameHub");
app.Run();

public class GameHub : Hub
{
    private static Dictionary<string, Player> Players = new();

    public async Task Join(string name, bool isDog)
    {
        Players[Context.ConnectionId] = new Player { 
            Id = Context.ConnectionId, 
            Name = name, 
            IsDog = isDog,
            Size = isDog ? 20 : 10 
        };
        await Clients.All.SendAsync("update", Players.Values);
    }

    public async Task Move(double x, double y)
    {
        if (Players.TryGetValue(Context.ConnectionId, out var player))
        {
            player.X = x;
            player.Y = y;
            await Clients.All.SendAsync("update", Players.Values);
        }
    }

    public async Task Maul(string targetId)
    {
        if (Players.TryGetValue(Context.ConnectionId, out var attacker) && 
            Players.TryGetValue(targetId, out var target))
        {
            attacker.Size += target.Size / 2;
            Players.Remove(targetId);
            await Clients.All.SendAsync("update", Players.Values);
        }
    }
}
public class Player
{
    public string Id { get; set; }
    public string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Size { get; set; }
    public bool IsDog { get; set; }
}
