using UnityEngine;
using Mirror;
using UnityEngine.UI;
using System.Collections;

public class MaulingMinigame : NetworkBehaviour
{
    public float duration = 5f;
    public float clickThreshold = 20f;
    
    [SerializeField]
    private Slider progressBar;
    [SerializeField]
    private Text clickCountText;
    
    private DogController attacker;
    private DogController defender;
    private float clickCount;
    private float timeLeft;
    private bool isActive;

    public void InitializeMinigame(DogController _attacker, DogController _defender)
    {
        attacker = _attacker;
        defender = _defender;
        
        RpcStartMinigame();
    }

    [ClientRpc]
    void RpcStartMinigame()
    {
        isActive = true;
        timeLeft = duration;
        clickCount = 0;
        
        if (progressBar) progressBar.gameObject.SetActive(true);
        if (clickCountText) clickCountText.gameObject.SetActive(true);
        
        StartCoroutine(MinigameTimer());
    }

    void Update()
    {
        if (!isActive) return;

        if (Input.GetMouseButtonDown(0))
        {
            clickCount++;
            UpdateUI();
        }
    }

    void UpdateUI()
    {
        if (progressBar) progressBar.value = timeLeft / duration;
        if (clickCountText) clickCountText.text = $"Clicks: {clickCount}";
    }

    IEnumerator MinigameTimer()
    {
        while (timeLeft > 0)
        {
            timeLeft -= Time.deltaTime;
            UpdateUI();
            yield return null;
        }
        
        EndMinigame();
    }

    [Server]
    void EndMinigame()
    {
        isActive = false;
        bool attackerWon = clickCount >= clickThreshold;
        
        if (attackerWon)
        {
            float sizeGain = defender.size * 0.2f;
            attacker.RpcGrow(sizeGain);
            defender.RpcGrow(-sizeGain);
        }
        
        RpcCleanupMinigame();
    }

    [ClientRpc]
    void RpcCleanupMinigame()
    {
        if (progressBar) progressBar.gameObject.SetActive(false);
        if (clickCountText) clickCountText.gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
