using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using UnityEditor.Experimental.GraphView;

public class PlayerController : MonoBehaviour
{
    // REFERENCES
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference bombAction;
    private Rigidbody2D rb;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private TMP_Text bombCounterText;

    //RAGDOLL
    [SerializeField] private ShortstuffRagdoll ragdollPrefab;
    private ShortstuffRagdoll activeRagdoll;
    private bool stunned;
    private Collider2D[] playerColliders;
    [SerializeField] private SpriteRenderer characterSprite;

    //STUN SETTINGS
    [SerializeField] private float minimumStunTime = 1.5f;
    [SerializeField] private float maximumStunTime = 4f;
    [SerializeField] private float recoveryCheckRadius = 0.22f;
    [SerializeField] private float recoveryHeight = 0.15f;

    private Vector2 standingColliderSize;
    private Vector2 standingColliderOffset;

    // BOMB REFERENCES
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private Transform bombSpawnPoint;

    [SerializeField] private float throwForce = 9f;

    [Range(0f, 90f)]
    [SerializeField] private float throwAngle = 40f;

    [SerializeField] private float throwSpin = 360f;

    private Bomb activeBomb;

    // PLAYER INPUT
    private float moveInput;
    private bool jumpRequested = false;
    private bool grounded;

    [SerializeField] private InputActionReference detonateAction;

    // MOVEMENT SETTINGS
    [Range(1f, 12f)]
    [SerializeField] private float moveSpeed = 6f;

    [Range(1f, 100f)]
    [SerializeField] private float acceleration = 40f;

    private float currentSpeed;

    [Range(1f, 20f)]
    [SerializeField] private float jumpForce = 10f;

    [Range(0.05f, 0.5f)]
    [SerializeField] private float groundCheckRadius = 0.15f;

    [Range(0f, 1f)]
    [SerializeField] private float airControl = 0.3f;

    public int FacingDirection { get; private set; } = 1;
    [SerializeField] private bool spriteFacesRight = true;

    // SETUP

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        playerColliders = GetComponentsInChildren<Collider2D>();

        Collider2D mainCollider = GetComponent<Collider2D>();

        if (mainCollider != null)
        {
            standingColliderSize = mainCollider.bounds.size;

            standingColliderOffset = (Vector2)(mainCollider.bounds.center - transform.position);
        }

        currentBombs = maxBombs;
    }

    private void Start()
    {
        UpdateBombUI();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();


        jumpAction.action.Enable();
        jumpAction.action.performed += OnJump;

        bombAction.action.Enable();
        bombAction.action.performed += OnBomb;

        if (detonateAction != null)
        {
            detonateAction.action.Enable();
            detonateAction.action.performed += OnDetonate;
        }
    }

    private void OnDisable()
    {
        moveAction.action.Disable();


        jumpAction.action.performed -= OnJump;
        jumpAction.action.Disable();

        
        bombAction.action.performed -= OnBomb;
        bombAction.action.Disable();

        if (detonateAction != null)
        {
            detonateAction.action.performed -= OnDetonate;
            detonateAction.action.Disable();
        }
    }


    // INPUT
    private void Update()
    {
        if (stunned) return;

        moveInput = moveAction.action.ReadValue<Vector2>().x;

        if (moveInput > 0.1f)
        {
            FacingDirection = 1;
        }
        else if (moveInput < -0.1f)
        {
            FacingDirection = -1;
        }

        if (characterSprite != null)
        {
            characterSprite.flipX = spriteFacesRight
                ? FacingDirection < 0
                : FacingDirection > 0;
        }
    }

    // MOVEMENT
    private void FixedUpdate()
    {
        if (stunned) return;

        CheckGround();
        HandleAcceleration();
        HandleMovement();
        HandleJump();
    }

    // HANDLE ACCELLERATION
    private void HandleAcceleration()
    {
        // Set "Target speed" (Ideal speed if it were in vacuum)
        float targetSpeed = moveInput * moveSpeed;

        float currentAcceleration = CalculateAcceleration();

        // Calculate currentspeed based on target speed but with acceleration damnpening)
        currentSpeed = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, currentAcceleration * Time.fixedDeltaTime);
    }


    // HANDLE MOVEMENT
    private void HandleMovement()
    {
        rb.linearVelocity = new Vector2(currentSpeed, rb.linearVelocity.y);
    }

    // HANDLE JUMPING
    private void OnJump(InputAction.CallbackContext context)
    {
        if (stunned) return;

        jumpRequested = true;
    }

    private void HandleJump()
    {
        if (jumpRequested && grounded)
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }

        jumpRequested = false;
    }

    // CHECK GROUNDING

    private void CheckGround()
    {
        grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;
    }

    // CALCULATE ACCELERATION
    private float CalculateAcceleration()
    {
        if (grounded) // check if grounded
        {
            return acceleration; // just use normal acceleration
        }

        return acceleration * airControl; // if airborne, use acceleration times by a percentage (30%)
    }

    // BOMBS
    private void OnBomb(InputAction.CallbackContext context)
    {
        if (stunned) return;

        ThrowBomb();
    }

    private void ThrowBomb()
    {
        if (activeBomb != null)
        {
            return;
        }
        
        if (currentBombs <= 0)
        {
            return;
        }

        float radians = throwAngle = throwAngle * Mathf.Deg2Rad;

        Vector2 direction = new Vector2(
            FacingDirection * Mathf.Cos(radians),
            Mathf.Sin(radians)
        );

        GameObject instance = Instantiate(
            bombPrefab,
            bombSpawnPoint.position,
            Quaternion.identity
        );

        Bomb bomb = instance.GetComponent<Bomb>();

        activeBomb = bomb;

        activeBomb.Exploded += OnBombExploded;

        if (bomb != null)
        {
            bomb.Launch(
                direction * throwForce,
                -FacingDirection * throwSpin
            );
        }

        currentBombs--;
        UpdateBombUI();
    }

    private void OnBombExploded(Bomb bomb)
    {
        if (activeBomb == bomb)
        {
            activeBomb.Exploded -= OnBombExploded;
            activeBomb = null;
        }
    }

    private void OnDetonate(InputAction.CallbackContext contect)
    {
        if (stunned) return;

        if (activeBomb != null)
        {
            activeBomb.Detonate();
        }
    }

    //BOMB INVENTORY
    [Range(1,10)]
    [SerializeField] private int maxBombs = 3;

    private int currentBombs;

    public void RefillBombs()
    {
        currentBombs = maxBombs;
        UpdateBombUI();
    }

    //USER INTERFACE

    private void UpdateBombUI()
    {
        bombCounterText.text = "BOMBS: " + currentBombs;
    }

    public void ReceiveExplosion(Vector2 explosionOrigin, Vector2 impulse)
    {
        if (stunned) return;

        stunned = true;

        Vector2 previousVelocity = rb.linearVelocity;

        activeRagdoll = Instantiate(
            ragdollPrefab,
            transform.position,
            Quaternion.identity
        );

        activeRagdoll.Activate(
            previousVelocity,
            impulse,
            explosionOrigin
        );

        if (characterSprite != null)
        {
            characterSprite.enabled = false;
        }

        foreach (Collider2D col in playerColliders)
        {
            col.enabled = false;
        }

        rb.simulated = false;

        moveInput = 0f;
        jumpRequested = false;

        StartCoroutine(RecoverWhenReady());
    }

    private IEnumerator RecoverWhenReady()
    {
        yield return new WaitForSeconds(minimumStunTime);

        float elapsed = minimumStunTime;

        while (activeRagdoll != null)
        {
            Vector2 torsoPosition = activeRagdoll.TorsoPosition;

            bool nearGround = Physics2D.OverlapCircle(
                torsoPosition + Vector2.down * 0.55f,
                recoveryCheckRadius,
                groundLayer
            ) != null;

            bool slowEnough = activeRagdoll.TorsoSpeed < 2f;

            if (nearGround && (slowEnough || elapsed >= maximumStunTime))
            {
                Vector2 candidate = torsoPosition + Vector2.up * recoveryHeight;

                bool blocked = Physics2D.OverlapBox(
                    candidate + standingColliderOffset,
                    standingColliderSize * 0.5f,
                    0f,
                    groundLayer
                ) != null;

                if (!blocked)
                {
                    FinishRecovery(candidate);
                    yield break;
                }
            }

            elapsed += 0.1f;

            yield return new WaitForSeconds(0.1f);
        }
    }

    private void FinishRecovery(Vector2 position)
    {
        Destroy(activeRagdoll.gameObject);

        activeRagdoll = null;

        transform.position = position;

        rb.simulated = true;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        foreach (Collider2D col in playerColliders)
        {
            col.enabled = true;
        }

        if (characterSprite != null)
        {
            characterSprite.enabled = true;
        }

        currentSpeed = 0f;
        moveInput = 0f;
        jumpRequested = false;
        stunned = false;
    }




    // GIZMOS AND DEBUGS

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
