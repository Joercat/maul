using System.Net.WebSockets;
using System.Text.Json;

namespace MaulingSimulator;

public class Program
{
    private static GameState gameState = new();
    private static Dictionary<string, WebSocket> clients = new();
    private static Dictionary<string, MiniGame> activeMinigames = new();

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
        var player = new Player { Id = playerId, X = 0, Y = 0 };
        gameState.Players.Add(playerId, player);

        var buffer = new byte[1024];
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
        finally
        {
            gameState.Players.Remove(playerId);
            clients.Remove(playerId);
        }
    }

    private static async Task ProcessMessageAsync(string playerId, string message)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(message);
        string action = data["action"].ToString();

        switch (action)
        {
            case "move":
                UpdatePlayerPosition(playerId, float.Parse(data["x"].ToString()), float.Parse(data["y"].ToString()));
                break;
            case "attack":
                string targetId = data["targetId"].ToString();
                await InitiateMiniGame(playerId, targetId);
                break;
            case "miniGameInput":
                ProcessMiniGameInput(playerId);
                break;
        }

        await BroadcastGameState();
    }

    private static void UpdatePlayerPosition(string playerId, float x, float y)
    {
        var player = gameState.Players[playerId];
        player.X = x;
        player.Y = y;

        CheckPowerUpCollisions(player);
        CheckNPCCollisions(player);
    }

    private static void CheckPowerUpCollisions(Player player)
    {
        foreach (var powerUp in gameState.PowerUps.ToList())
        {
            if (IsColliding(player.X, player.Y, powerUp.X, powerUp.Y))
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
            case PowerUpType.Invincibility:
                player.IsInvincible = true;
                player.InvincibleEndTime = DateTime.Now.AddSeconds(5);
                break;
            case PowerUpType.SpeedBoost:
                player.HasSpeedBoost = true;
                player.Speed *= 2;
                player.SpeedBoostEndTime = DateTime.Now.AddSeconds(5);
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
            if (IsColliding(player.X, player.Y, npc.X, npc.Y))
            {
                player.Level = Math.Min(100, player.Level + 1);
                gameState.NPCs.Remove(npc);
            }
        }
    }

    private static bool IsColliding(float x1, float y1, float x2, float y2)
    {
        const float collisionDistance = 50f;
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2)) < collisionDistance;
    }

    private static async Task InitiateMiniGame(string attackerId, string defenderId)
    {
        var attacker = gameState.Players[attackerId];
        var defender = gameState.Players[defenderId];

        float difficulty = (float)attacker.Level / defender.Level;
        
        var miniGame = new MiniGame
        {
            AttackerId = attackerId,
            DefenderId = defenderId,
            Progress = 0.5f,
            StartTime = DateTime.Now,
            Difficulty = difficulty
        };

        activeMinigames[attackerId] = miniGame;
        await BroadcastGameState();
    }

    private static void ProcessMiniGameInput(string playerId)
    {
        if (activeMinigames.TryGetValue(playerId, out var miniGame))
        {
            bool isAttacker = playerId == miniGame.AttackerId;
            bool attackerWins = miniGame.ProcessInput(playerId, isAttacker);

            if (attackerWins)
            {
                var attacker = gameState.Players[miniGame.AttackerId];
                attacker.Level = Math.Min(100, attacker.Level + (isAttacker ? 2 : 3));
                activeMinigames.Remove(playerId);
            }
        }
    }

    private static async Task BroadcastGameState()
    {
        var state = JsonSerializer.Serialize(gameState);
        var buffer = System.Text.Encoding.UTF8.GetBytes(state);
        
        foreach (var client in clients)
        {
            if (client.Value.State == WebSocketState.Open)
            {
                await client.Value.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
        }
    }

    private static void StartPeriodicTasks()
    {
        _ = SpawnPowerUps();
        _ = SpawnNPCs();
    }

    private static async Task SpawnPowerUps()
    {
        var random = new Random();
        while (true)
        {
            if (gameState.PowerUps.Count < 5)
            {
                gameState.PowerUps.Add(new PowerUp
                {
                    X = random.Next(50, 750),
                    Y = random.Next(50, 550),
                    Type = (PowerUpType)random.Next(3)
                });
                await BroadcastGameState();
            }
            await Task.Delay(10000); // Spawn every 10 seconds
        }
    }

    private static async Task SpawnNPCs()
    {
        var random = new Random();
        while (true)
        {
            if (gameState.NPCs.Count < 10)
            {
                gameState.NPCs.Add(new NPC
                {
                    X = random.Next(50, 750),
                    Y = random.Next(50, 550),
                    Size = random.Next(1, 4)
                });
                await BroadcastGameState();
            }
            await Task.Delay(5000); // Spawn every 5 seconds
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
    public float Speed { get; set; } = 5f;
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
    Invincibility,
    SpeedBoost,
    DoublePower
}

public class NPC
{
    public float X { get; set; }
    public float Y { get; set; }
    public int Size { get; set; }
}

public class MiniGame
{
    public string AttackerId { get; set; }
    public string DefenderId { get; set; }
    public float Progress { get; set; }
    public DateTime StartTime { get; set; }
    public float Difficulty { get; set; }

    public bool ProcessInput(string playerId, bool isAttacker)
    {
        float increment = isAttacker ? 0.1f : -0.1f;
        increment *= Difficulty;
        Progress += increment;

        if (Progress >= 1f) return true;
        if (Progress <= 0f) return false;

        return false;
    }
}
