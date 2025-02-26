using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class CustomNetworkManager : NetworkManager
{
    [Header("Game Settings")]
    public int minPlayersToStart = 2;
    public Vector2 spawnAreaSize = new Vector2(20f, 20f);
    
    private HashSet<NetworkConnection> readyPlayers = new HashSet<NetworkConnection>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("Server Started!");
    }

    public override void OnServerConnect(NetworkConnection conn)
    {
        base.OnServerConnect(conn);
        
        if (numPlayers >= maxConnections)
        {
            conn.Disconnect();
            return;
        }
    }

    public override void OnServerAddPlayer(NetworkConnection conn)
    {
        Vector3 spawnPos = GetRandomSpawnPosition();
        GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        
        NetworkServer.AddPlayerForConnection(conn, player);
        
        DogController dog = player.GetComponent<DogController>();
        if (dog != null)
        {
            GameManager.Instance.RegisterPlayer(conn, dog);
        }
    }

    public override void OnServerDisconnect(NetworkConnection conn)
    {
        GameManager.Instance.UnregisterPlayer(conn);
        base.OnServerDisconnect(conn);
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("Connected to server!");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("Disconnected from server!");
    }

    Vector3 GetRandomSpawnPosition()
    {
        float x = Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2);
        float z = Random.Range(-spawnAreaSize.y / 2, spawnAreaSize.y / 2);
        
        Vector3 spawnPos = new Vector3(x, 0, z);
        
        if (Physics.Raycast(spawnPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit))
        {
            return hit.point + Vector3.up * 0.5f;
        }
        
        return spawnPos;
    }

    [Server]
    public void PlayerReady(NetworkConnection conn)
    {
        readyPlayers.Add(conn);
        
        if (readyPlayers.Count >= minPlayersToStart)
        {
            StartGame();
        }
    }

    [Server]
    void StartGame()
    {
        GameManager.Instance.StartGame();
        readyPlayers.Clear();
    }
}
