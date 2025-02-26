complete implementations:

using UnityEngine;
using Mirror;

public class DogController : NetworkBehaviour
{
    [SyncVar]
    public float size = 1f;
    public float moveSpeed = 10f;
    public float rotationSpeed = 100f;
    private Rigidbody rb;
    private Animator animator;
    
    [SerializeField]
    private ParticleSystem maulEffect;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        transform.localScale = Vector3.one * size;
        
        if (isLocalPlayer)
        {
            Camera.main.GetComponent<CameraFollow>().SetTarget(transform);
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 movement = new Vector3(horizontal, 0f, vertical);
        
        if (movement != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(movement, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
            animator.SetBool("IsRunning", true);
        }
        else
        {
            animator.SetBool("IsRunning", false);
        }
        
        rb.MovePosition(rb.position + movement * moveSpeed * Time.deltaTime);
    }

    [Command]
    public void CmdStartMauling(NetworkIdentity target)
    {
        if (target.TryGetComponent<DogController>(out DogController targetDog))
        {
            StartMaulingMinigame(targetDog);
        }
        else if (target.TryGetComponent<Victim>(out Victim victim))
        {
            MaulVictim(victim);
        }
    }

    void StartMaulingMinigame(DogController opponent)
    {
        MaulingMinigame minigame = GameManager.Instance.CreateMinigame();
        minigame.InitializeMinigame(this, opponent);
    }

    [ClientRpc]
    public void RpcGrow(float amount)
    {
        size += amount;
        transform.localScale = Vector3.one * size;
        if (maulEffect) maulEffect.Play();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isLocalPlayer) return;
        
        if (collision.gameObject.TryGetComponent<NetworkIdentity>(out NetworkIdentity target))
        {
            CmdStartMauling(target);
        }
    }
}
