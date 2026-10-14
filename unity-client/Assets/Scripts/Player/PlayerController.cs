using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 8f;
    public float jumpDelay = 0.2f;

    private Rigidbody2D rb;
    private Animator anim;
    private float currentMoveX;
    private bool isGrounded = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    void OnEnable()
    {
        GameEvents.OnMoveInput += HandleMove;
        GameEvents.OnJumpPressed += HandleJump;
    }

    void OnDisable()
    {
        GameEvents.OnMoveInput -= HandleMove;
        GameEvents.OnJumpPressed -= HandleJump;
    }


    // 1. 处理移动输入，更新动画状态和角色朝向
    private void HandleMove(float moveX)
    {
        currentMoveX = moveX;

        if (anim != null)
        {
            anim.SetFloat("Speed", Mathf.Abs(moveX));
        }

        if (moveX > 0.1f)
        {
            transform.localScale = new Vector3(1, 1, 1);
        }
        else if (moveX < -0.1f)
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
    }

    // 2. 处理跳跃输入，触发跳跃动画并在短暂延迟后施加跳跃力
    private void HandleJump()
    {
        if (isGrounded)
        {
            StartCoroutine(JumpWithDelay());
        }
    }

    // 在 FixedUpdate 中应用水平移动，保持物理一致性
    void FixedUpdate()
    {
        rb.velocity = new Vector2(currentMoveX * moveSpeed, rb.velocity.y);
    }

    // 跳跃协程：先设置为非地面状态，触发跳跃动画，等待短暂延迟后施加跳跃力
    IEnumerator JumpWithDelay()
    {
        isGrounded = false;

        if (anim != null)
        {
            anim.SetBool("IsJumping", true);
        }

        yield return new WaitForSeconds(jumpDelay);

        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.contacts[0].normal.y > 0.5f)
        {
            isGrounded = true;
            if (anim != null)
            {
                anim.SetBool("IsJumping", false);
            }
        }
    }
}
