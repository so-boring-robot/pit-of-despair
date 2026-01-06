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
    public float dashDuration = 0.2f;

    [Header("Wall Jump")]
    public float wallJumpForce = 15f;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private bool isGrounded;
    private bool isTouchingWall;
    private bool isDashing;
    private bool isWallJumping;
    private bool blockTowardsWall;

    private Vector2 wallNormal;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (!isDashing)
            MovePlayer();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        }
        else if (isTouchingWall)
        {
            WallJump();
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.performed && !isDashing)
            StartCoroutine(Dash());
    }

    private IEnumerator Dash()
    {
        isDashing = true;
        rb.velocity = moveInput.normalized * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        rb.velocity = new Vector2(moveInput.x * moveSpeed, rb.velocity.y);
        isDashing = false;
    }

    private void WallJump()
    {
        isWallJumping = true;

        float direction = wallNormal.x > 0 ? 1f : -1f;
        rb.velocity = new Vector2(direction * wallJumpForce, wallJumpForce);

        StartCoroutine(WallJumpCooldown());
    }

    private IEnumerator WallJumpCooldown()
    {
        blockTowardsWall = true;
        yield return new WaitForSeconds(0.15f);
        blockTowardsWall = false;
        isWallJumping = false;
    }

    private void MovePlayer()
    {
        if (isWallJumping)
            return;

        float inputX = moveInput.x;

        if (blockTowardsWall && isTouchingWall)
        {
            if (Mathf.Sign(inputX) == -Mathf.Sign(wallNormal.x))
                inputX = 0;
        }

        rb.velocity = new Vector2(inputX * moveSpeed, rb.velocity.y);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Vector2 n = collision.contacts[0].normal;

        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            return;
        }

        if (collision.gameObject.CompareTag("Wall"))
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
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = false;

        if (collision.gameObject.CompareTag("Wall"))
        {
            isTouchingWall = false;
            wallNormal = Vector2.zero;
            isGrounded = false;
        }
    }
}
