using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class VictimSpawner : NetworkBehaviour
{
    public GameObject victimPrefab;
    public float spawnInterval = 2f;
    public float spawnRadius = 20f;
    public int maxVictims = 20;
    
    private List<GameObject> activeVictims = new List<GameObject>();

    public override void OnStartServer()
    {
        InvokeRepeating(nameof(SpawnVictim), 0f, spawnInterval);
    }

    [Server]
    void SpawnVictim()
    {
        if (activeVictims.Count >= maxVictims) return;
        
        // Clean up destroyed victims from the list
        activeVictims.RemoveAll(victim => victim == null);
        
        Vector2 randomCircle = Random.insideUnitCircle.normalized * spawnRadius;
        Vector3 spawnPos = new Vector3(randomCircle.x, 0f, randomCircle.y);
        
        // Ensure spawn position is valid
        if (Physics.Raycast(spawnPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit))
        {
            spawnPos = hit.point;
        }
        
        GameObject victim = Instantiate(victimPrefab, spawnPos, Quaternion.identity);
        NetworkServer.Spawn(victim);
        activeVictims.Add(victim);
    }

    public override void OnStopServer()
    {
        CancelInvoke();
        foreach (GameObject victim in activeVictims)
        {
            if (victim != null)
            {
                NetworkServer.Destroy(victim);
            }
        }
        activeVictims.Clear();
    }
}
