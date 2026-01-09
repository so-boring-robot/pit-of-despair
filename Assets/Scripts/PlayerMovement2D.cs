using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerMovement2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.6f;
    public TrailRenderer dashTrail;

    [Header("Air Dash Limit")]
    public bool hasAirDash = true;

    [Header("Wall Jump")]
    public float wallJumpForce = 15f;
    public float wallJumpControlDelay = 0.2f;

    [Header("Wall Slide")] 
    public float wallSlideSpeed = 2f;

    [Header("Ground Check (optionnel)")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Wall Check (optionnel)")]
    public Transform wallCheck;
    public float wallCheckDistance = 0.2f;
    public LayerMask wallLayer;

    [Header("Coyote Time")]
    public float coyoteTime = 0.1f;
    private float coyoteCounter;

    [Header("Jump Buffer")]
    public float jumpBufferTime = 0.1f;
    private float jumpBufferCounter;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private bool isGrounded;
    private bool isTouchingWall;
    private bool isDashing;
    private bool isWallJumping;
    private bool blockTowardsWall;
    private bool canDash = true;

    private Vector2 wallNormal;

    // Fallbacks si les checks avancés ne sont pas configurés
    private bool useCollisionGround = true;
    private bool useCollisionWall = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (dashTrail != null)
            dashTrail.emitting = false;

        if (groundCheck != null)
            useCollisionGround = false;

        if (wallCheck != null)
            useCollisionWall = false;
    }

    void Update()
    {
        // Ground check avancé si configuré
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        // Wall check avancé si configuré
        if (wallCheck != null)
        {
            Vector2 dir = Vector2.right * Mathf.Sign(transform.localScale.x);
            RaycastHit2D hit = Physics2D.Raycast(wallCheck.position, dir, wallCheckDistance, wallLayer);
            if (hit.collider != null)
            {
                isTouchingWall = true;
                wallNormal = hit.normal;
            }
            else
            {
                isTouchingWall = false;
                wallNormal = Vector2.zero;
            }
        }

        // Coyote time
        if (isGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter -= Time.deltaTime;

        // Jump buffer
        jumpBufferCounter -= Time.deltaTime;

        // Jump déclenché si buffer + coyote
        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpBufferCounter = 0f;
        }

        // Réinitialisation de l'air-dash
        if (isGrounded || isTouchingWall)
            hasAirDash = true;

        // Wall slide
        if (!isGrounded && isTouchingWall && !isDashing && !isWallJumping)
        {
            if (rb.velocity.y < -wallSlideSpeed)
                rb.velocity = new Vector2(rb.velocity.x, -wallSlideSpeed);
        }

        if (!isDashing)
            MovePlayer();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // Intention de saut (jump buffer)
            jumpBufferCounter = jumpBufferTime;

            // Wall jump prioritaire si en l'air et contre un mur
            if (!isGrounded && isTouchingWall)
            {
                WallJump();
                jumpBufferCounter = 0f;
            }
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        // Si on est en l'air et qu'on a déjà utilisé l'air-dash → interdit
        if (!isGrounded && !isTouchingWall && !hasAirDash)
            return;

        if (canDash && !isDashing)
        {
            // Si on dash en l'air → on consomme l'air-dash
            if (!isGrounded && !isTouchingWall)
                hasAirDash = false;

            StartCoroutine(Dash());
        }
    }

    private IEnumerator Dash()
    {
        canDash = false;
        isDashing = true;

        // Momentum cancel
        rb.velocity = Vector2.zero;

        Vector2 dashDir;
        // Si on est collé à un mur → dash automatiquement dans la direction opposée
        if (isTouchingWall && wallNormal != Vector2.zero)
        {
            dashDir = new Vector2(Mathf.Sign(wallNormal.x), 0f);
        }
        else
        {
            // Sinon dash normal (input ou direction du regard)
            dashDir = moveInput.sqrMagnitude > 0.01f
                ? moveInput.normalized
                : new Vector2(Mathf.Sign(transform.localScale.x), 0f);
        }


        // Petit freeze frame pour le feeling
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(0.05f);
        Time.timeScale = originalTimeScale;

        // Trail visuel
        if (dashTrail != null)
            dashTrail.emitting = true;

        rb.velocity = dashDir * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        if (dashTrail != null)
            dashTrail.emitting = false;

        // Retour au contrôle normal
        rb.velocity = new Vector2(moveInput.x * moveSpeed, rb.velocity.y);
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private void WallJump()
    {
        isWallJumping = true;

        // La normale pointe déjà "loin du mur"
        // mur à gauche → normal.x > 0 → on saute à droite (x > 0)
        // mur à droite → normal.x < 0 → on saute à gauche (x < 0)
        float jumpDirection = Mathf.Sign(wallNormal.x);

        rb.velocity = new Vector2(jumpDirection * wallJumpForce, wallJumpForce);

        StartCoroutine(WallJumpControlLock());
    }

    private IEnumerator WallJumpControlLock()
    {
        blockTowardsWall = true;
        yield return new WaitForSeconds(wallJumpControlDelay);
        blockTowardsWall = false;
        isWallJumping = false;
    }

    private void MovePlayer()
    {
        if (isWallJumping)
            return;

        float inputX = moveInput.x;

        // Empêche de pousser vers le mur juste après un wall jump
        if (blockTowardsWall && isTouchingWall && wallNormal != Vector2.zero)
        {
            // Si on pousse vers le mur (même signe que -normal), on bloque
            if (Mathf.Sign(inputX) == -Mathf.Sign(wallNormal.x))
                inputX = 0;
        }

        rb.velocity = new Vector2(inputX * moveSpeed, rb.velocity.y);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Vector2 n = collision.contacts[0].normal;

        if (useCollisionGround && collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }

        if (useCollisionWall && collision.gameObject.CompareTag("Wall"))
        {
            if (n.y > 0.9f)
            {
                isGrounded = true;
                return;
            }

            if (Mathf.Abs(n.x) > 0.95f)
            {
                isTouchingWall = true;
                wallNormal = n;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (useCollisionGround && collision.gameObject.CompareTag("Ground"))
            isGrounded = false;

        if (useCollisionWall && collision.gameObject.CompareTag("Wall"))
        {
            isTouchingWall = false;
            wallNormal = Vector2.zero;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (wallCheck != null)
        {
            Gizmos.color = Color.blue;
            Vector3 dir = Vector3.right * Mathf.Sign(transform.localScale.x) * wallCheckDistance;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + dir);
        }
    }
}
