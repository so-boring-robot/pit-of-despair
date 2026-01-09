using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerMovement2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    [Header("Advanced Movement (Hollow Knight Style)")]
    public float acceleration = 60f;          // accélération au sol
    public float deceleration = 50f;          // décélération au sol
    public float airAcceleration = 30f;       // accélération en l'air
    public float airDeceleration = 25f;       // décélération en l'air
    public float airControlMultiplier = 0.6f; // contrôle réduit en l'air

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

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Wall Check")]
    public Transform wallCheckLeft;
    public Transform wallCheckRight;
    public float wallCheckDistance = 0.2f;
    public LayerMask wallLayer;

    [Header("Coyote Time")]
    public float coyoteTime = 0.1f;
    public float coyoteCounter;

   [Header("Variable Jump")]
    public float jumpCutMultiplier = 0.5f;        // coupe le saut si relâché tôt
    public float fallGravityMultiplier = 2f;      // accélère la chute
    public float lowJumpGravityMultiplier = 2.5f; // chute plus rapide si saut court

    [Header("Jump Buffer")]
    public float jumpBufferTime = 0.1f;
    public float jumpBufferCounter;

    [Header ("Skills")]
    public Rigidbody2D rb;
    public Vector2 moveInput;
    public bool isGrounded;
    public bool isTouchingWall;
    public bool isDashing;
    public bool isWallJumping;
    public bool blockTowardsWall;
    public bool canDash = true;
    public Vector2 wallNormal;

    [Header("Landing Smoothing")]
    public float landingSmoothingTime = 0.08f; // durée de l’amorti
    public float landingMaxFallSpeed = -12f;   // vitesse max considérée comme "forte chute"
    private bool isLandingSmoothing = false;

    [Header("Landing Freeze Frame")]
    public float landingFreezeDuration = 0.03f;
    public ParticleSystem landingDust;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (dashTrail != null)
            dashTrail.emitting = false;
    }

    void Update()
    {   
        // Détection d'atterrissage
        bool wasGrounded = isGrounded;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (!wasGrounded && isGrounded)
        {
            float fallSpeed = rb.velocity.y;

            // Debug pour vérifier
            Debug.Log("Landing detected with fall speed: " + fallSpeed);

            // Seulement si la chute est assez grande
            if (fallSpeed < landingMaxFallSpeed)
            {
                if (landingDust != null)
                {
                    landingDust.transform.position = groundCheck.position;
                    landingDust.Play();
                }

                StartCoroutine(LandingFreezeFrame());
                StartCoroutine(LandingSmoothing());
            }
        }

        // --- Ground Check ---
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // --- Wall Check (gauche + droite) ---
        bool leftWall = Physics2D.Raycast(wallCheckLeft.position, Vector2.left, wallCheckDistance, wallLayer);
        bool rightWall = Physics2D.Raycast(wallCheckRight.position, Vector2.right, wallCheckDistance, wallLayer);

        if (leftWall)
        {
            isTouchingWall = true;
            wallNormal = Vector2.right; // mur à gauche → normale vers la droite
        }
        else if (rightWall)
        {
            isTouchingWall = true;
            wallNormal = Vector2.left; // mur à droite → normale vers la gauche
        }
        else
        {
            isTouchingWall = false;
            wallNormal = Vector2.zero;
        }

        // --- Coyote Time ---
        if (isGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter -= Time.deltaTime;

        // Jump buffer
        jumpBufferCounter -= Time.deltaTime;

        // Empêche le buffer de dépasser la valeur max
        if (jumpBufferCounter > jumpBufferTime)
            jumpBufferCounter = jumpBufferTime;

        // Empêche le buffer de rester actif dans des états où il ne doit pas servir
        if (isTouchingWall || isDashing || isWallJumping)
            jumpBufferCounter = Mathf.Min(jumpBufferCounter, 0.02f); // expire quasi immédiatement

        // Clamp final
        jumpBufferCounter = Mathf.Clamp(jumpBufferCounter, 0f, jumpBufferTime);

        // Saut normal (pas sur un mur)
        if (jumpBufferCounter > 0f && coyoteCounter > 0f && !isTouchingWall)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpBufferCounter = 0f;
        }

        // --- Reset Air Dash ---
        if (isGrounded || isTouchingWall)
            hasAirDash = true;

        // --- Wall Slide ---
        if (!isGrounded && isTouchingWall && !isDashing && !isWallJumping)
        {
            if (rb.velocity.y < -wallSlideSpeed)
                rb.velocity = new Vector2(rb.velocity.x, -wallSlideSpeed);
        }

        // Gravité dynamique (façon Hollow Knight / Celeste)
        if (!isGrounded && !isWallJumping && !isDashing)
        {
            // Chute normale → accélérée
            if (rb.velocity.y < 0)
            {
                rb.velocity += Vector2.up * Physics2D.gravity.y * (fallGravityMultiplier - 1) * Time.deltaTime;
            }
            // Saut court → chute encore plus rapide
            else if (rb.velocity.y > 0 && !Keyboard.current.spaceKey.isPressed)
            {
                rb.velocity += Vector2.up * Physics2D.gravity.y * (lowJumpGravityMultiplier - 1) * Time.deltaTime;
            }
        }



        if (!isDashing)
            MovePlayer();
    }

    private IEnumerator LandingFreezeFrame()
    {
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0.05f; // micro-pause
        yield return new WaitForSecondsRealtime(landingFreezeDuration);
        Time.timeScale = originalTimeScale;
    }


    private IEnumerator LandingSmoothing()
    {
        isLandingSmoothing = true;

        float timer = 0f;
        float initialYVel = rb.velocity.y;

        while (timer < landingSmoothingTime)
        {
            timer += Time.deltaTime;

            // interpolation douce vers 0
            float t = timer / landingSmoothingTime;
            float smoothVel = Mathf.Lerp(initialYVel, 0f, t);

            rb.velocity = new Vector2(rb.velocity.x, smoothVel);

            yield return null;
        }

        isLandingSmoothing = false;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            jumpBufferCounter = jumpBufferTime;

            // Wall jump prioritaire
            if (!isGrounded && isTouchingWall)
            {
                WallJump();
                jumpBufferCounter = 0f;
            }
        }
        if (context.canceled)
        {
            // Saut variable : coupe le saut si on relâche tôt
            if (rb.velocity.y > 0)
            {
                rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * jumpCutMultiplier);
            }
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (!isGrounded && !isTouchingWall && !hasAirDash)
            return;

        if (canDash && !isDashing)
        {
            if (!isGrounded && !isTouchingWall)
                hasAirDash = false;

            StartCoroutine(Dash());
        }
    }

    private IEnumerator Dash()
    {
        canDash = false;
        isDashing = true;

        rb.velocity = Vector2.zero;

        Vector2 dashDir;

        // Dash automatique opposé au mur
        if (isTouchingWall && wallNormal != Vector2.zero)
        {
            dashDir = new Vector2(wallNormal.x, 0f);
        }
        else
        {
            dashDir = moveInput.sqrMagnitude > 0.01f
                ? moveInput.normalized
                : new Vector2(Mathf.Sign(transform.localScale.x), 0f);
        }

        // Freeze frame
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(0.05f);
        Time.timeScale = originalTimeScale;

        if (dashTrail != null)
            dashTrail.emitting = true;

        rb.velocity = dashDir * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        if (dashTrail != null)
            dashTrail.emitting = false;

        rb.velocity = new Vector2(moveInput.x * moveSpeed, rb.velocity.y);
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private void WallJump()
    {
        isWallJumping = true;

        float jumpDirection = wallNormal.x; // normale = direction opposée au mur

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
        if (isLandingSmoothing)
            return; // on bloque le mouvement pendant l’amorti

        if (isWallJumping)
            return;

        float targetSpeed = moveInput.x * moveSpeed;
        float speedDiff = targetSpeed - rb.velocity.x;

        float accelRate;

        if (isGrounded)
        {
            // Accélération ou décélération au sol
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
        }
        else
        {
            // Contrôle aérien réduit
            float control = (Mathf.Abs(targetSpeed) > 0.01f) ? airAcceleration : airDeceleration;
            accelRate = control * airControlMultiplier;
        }

        float movement = accelRate * speedDiff * Time.deltaTime;

        rb.velocity = new Vector2(rb.velocity.x + movement, rb.velocity.y);
    }


    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (wallCheckLeft != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(wallCheckLeft.position,
                wallCheckLeft.position + Vector3.left * wallCheckDistance);
        }

        if (wallCheckRight != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(wallCheckRight.position,
                wallCheckRight.position + Vector3.right * wallCheckDistance);
        }
    }
}
