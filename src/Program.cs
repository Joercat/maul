using System.Net.WebSockets;
using System.Text.Json;

namespace MaulingSimulator;

public class Program
{
    private static GameState gameState = new();
    private static Dictionary<string, WebSocket> clients = new();
    private static Dictionary<string, MiniGame> activeMinigames = new();
    private static Random random = new();

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.UseStaticFiles();
        app.UseWebSockets();

        SetupRoutes(app);
        StartPeriodicTasks();

        app.Run("http://0.0.0.0:5000");
    }

    private static void SetupRoutes(WebApplication app)
    {
        app.MapGet("/", async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync("wwwroot/index.html");
        });

        app.Map("/ws", async context =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var ws = await context.WebSockets.AcceptWebSocketAsync();
                var playerId = Guid.NewGuid().ToString();
                clients.Add(playerId, ws);
                await HandleWebSocketConnection(playerId, ws);
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        });
    }

    private static async Task HandleWebSocketConnection(string playerId, WebSocket socket)
    {
        var buffer = new byte[4096];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessMessageAsync(playerId, message);
                }
            }
        }
        catch (WebSocketException)
        {
            // Connection closed
        }
        finally
        {
            if (gameState.Players.ContainsKey(playerId))
            {
                gameState.Players.Remove(playerId);
                clients.Remove(playerId);
                await BroadcastGameState();
            }
        }
    }

    private static async Task ProcessMessageAsync(string playerId, string message)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message);
        string action = data["action"].GetString();

        switch (action)
        {
            case "join":
                var name = data["name"].GetString();
                gameState.Players[playerId] = new Player 
                { 
                    Id = playerId,
                    Name = name,
                    X = random.Next(100, 1500),
                    Y = random.Next(100, 1100)
                };
                break;

            case "move":
                if (gameState.Players.TryGetValue(playerId, out var player))
                {
                    player.X = data["x"].GetSingle();
                    player.Y = data["y"].GetSingle();
                    CheckPowerUpCollisions(player);
                    CheckNPCCollisions(player);
                }
                break;

            case "attack":
                var targetId = data["targetId"].GetString();
                await InitiateAttack(playerId, targetId);
                break;
        }

        await BroadcastGameState();
    }

    private static void CheckPowerUpCollisions(Player player)
    {
        foreach (var powerUp in gameState.PowerUps.ToList())
        {
            if (IsColliding(player.X, player.Y, powerUp.X, powerUp.Y, 30))
            {
                ApplyPowerUp(player, powerUp);
                gameState.PowerUps.Remove(powerUp);
            }
        }
    }

    private static void ApplyPowerUp(Player player, PowerUp powerUp)
    {
        switch (powerUp.Type)
        {
            case PowerUpType.SpeedBoost:
                player.HasSpeedBoost = true;
                player.SpeedBoostEndTime = DateTime.UtcNow.AddSeconds(10);
                break;
            case PowerUpType.Invincibility:
                player.IsInvincible = true;
                player.InvincibleEndTime = DateTime.UtcNow.AddSeconds(5);
                break;
            case PowerUpType.DoublePower:
                player.HasDoublePower = true;
                break;
        }
    }

    private static void CheckNPCCollisions(Player player)
    {
        foreach (var npc in gameState.NPCs.ToList())
        {
            if (IsColliding(player.X, player.Y, npc.X, npc.Y, 40))
            {
                player.Level = Math.Min(100, player.Level + npc.Size);
                gameState.NPCs.Remove(npc);
            }
        }
    }

    private static bool IsColliding(float x1, float y1, float x2, float y2, float distance)
    {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2)) < distance;
    }

    private static async Task InitiateAttack(string attackerId, string defenderId)
    {
        if (!gameState.Players.ContainsKey(defenderId)) return;

        var attacker = gameState.Players[attackerId];
        var defender = gameState.Players[defenderId];

        if (defender.IsInvincible) return;

        float damage = attacker.HasDoublePower ? 2 : 1;
        defender.Level = Math.Max(1, defender.Level - (int)damage);
        attacker.Level = Math.Min(100, attacker.Level + 1);

        await BroadcastGameState();
    }

    private static async Task BroadcastGameState()
    {
        var state = JsonSerializer.Serialize(gameState);
        var buffer = System.Text.Encoding.UTF8.GetBytes(state);
        
        foreach (var client in clients.Where(c => c.Value.State == WebSocketState.Open))
        {
            try
            {
                await client.Value.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
            catch
            {
                // Client disconnected
            }
        }
    }

    private static void StartPeriodicTasks()
    {
        _ = SpawnPowerUps();
        _ = SpawnNPCs();
        _ = UpdatePowerUpEffects();
    }

    private static async Task SpawnPowerUps()
    {
        while (true)
        {
            if (gameState.PowerUps.Count < 5)
            {
                gameState.PowerUps.Add(new PowerUp
                {
                    X = random.Next(100, 1500),
                    Y = random.Next(100, 1100),
                    Type = (PowerUpType)random.Next(3)
                });
                await BroadcastGameState();
            }
            await Task.Delay(5000);
        }
    }

    private static async Task SpawnNPCs()
    {
        while (true)
        {
            if (gameState.NPCs.Count < 10)
            {
                gameState.NPCs.Add(new NPC
                {
                    X = random.Next(100, 1500),
                    Y = random.Next(100, 1100),
                    Size = random.Next(1, 4)
                });
                await BroadcastGameState();
            }
            await Task.Delay(3000);
        }
    }

    private static async Task UpdatePowerUpEffects()
    {
        while (true)
        {
            var now = DateTime.UtcNow;
            foreach (var player in gameState.Players.Values)
            {
                if (player.IsInvincible && now > player.InvincibleEndTime)
                    player.IsInvincible = false;
                
                if (player.HasSpeedBoost && now > player.SpeedBoostEndTime)
                    player.HasSpeedBoost = false;
            }
            await Task.Delay(100);
        }
    }
}

public class Player
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Level { get; set; } = 1;
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsInvincible { get; set; }
    public bool HasSpeedBoost { get; set; }
    public bool HasDoublePower { get; set; }
    public DateTime InvincibleEndTime { get; set; }
    public DateTime SpeedBoostEndTime { get; set; }
}

public class GameState
{
    public Dictionary<string, Player> Players { get; set; } = new();
    public List<PowerUp> PowerUps { get; set; } = new();
    public List<NPC> NPCs { get; set; } = new();
}

public class PowerUp
{
    public float X { get; set; }
    public float Y { get; set; }
    public PowerUpType Type { get; set; }
}

public enum PowerUpType
{
    SpeedBoost,
    Invincibility,
    DoublePower
}

public class NPC
{
    public float X { get; set; }
    public float Y { get; set; }
    public int Size { get; set; }
}
