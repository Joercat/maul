using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Game Settings")]
    public float gameDuration = 300f;
    public int maxPlayers = 10;
    public float minSizeToWin = 10f;
    
    [Header("Prefabs")]
    public GameObject maulingMinigamePrefab;
    public GameObject scoreboardUIPrefab;
    
    [SyncVar]
    private float currentGameTime;
    
    private Dictionary<NetworkConnection, DogController> players = new Dictionary<NetworkConnection, DogController>();
    private bool gameInProgress;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnStartServer()
    {
        currentGameTime = gameDuration;
        gameInProgress = true;
    }

    void Update()
    {
        if (!isServer || !gameInProgress) return;

        currentGameTime -= Time.deltaTime;
        
        if (currentGameTime <= 0)
        {
            EndGame();
        }

        CheckWinCondition();
    }

    public MaulingMinigame CreateMinigame()
    {
        GameObject minigameObj = Instantiate(maulingMinigamePrefab);
        NetworkServer.Spawn(minigameObj);
        return minigameObj.GetComponent<MaulingMinigame>();
    }

    [Server]
    public void RegisterPlayer(NetworkConnection conn, DogController dog)
    {
        players[conn] = dog;
        RpcUpdatePlayerCount(players.Count);
    }

    [Server]
    public void UnregisterPlayer(NetworkConnection conn)
    {
        if (players.ContainsKey(conn))
        {
            players.Remove(conn);
            RpcUpdatePlayerCount(players.Count);
        }
    }

    [ClientRpc]
    void RpcUpdatePlayerCount(int count)
    {
        Debug.Log($"Players in game: {count}");
    }

    void CheckWinCondition()
    {
        foreach (var player in players.Values)
        {
            if (player.size >= minSizeToWin)
            {
                EndGame(player);
                break;
            }
        }
    }

    [Server]
    void EndGame(DogController winner = null)
    {
        gameInProgress = false;
        RpcGameOver(winner ? winner.netId : 0);
    }

    [ClientRpc]
    void RpcGameOver(uint winnerNetId)
    {
        if (winnerNetId != 0)
        {
            Debug.Log($"Game Over! Winner: Player {winnerNetId}");
        }
        else
        {
            Debug.Log("Game Over! Time's up!");
        }
    }
}
