using UnityEngine;

/// <summary>
/// 集中输入管理器 - 读取所有输入源并通过 GameEvents 广播
/// 在场景中挂载此组件，并在 Inspector 中绑定 Joystick 和 UIButton
/// </summary>
public class InputManager : MonoBehaviour
{
    [Header("移动端控件")]
    public Joystick joystick;
    public UIButton jumpButton;
    public UIButton interactButton;

    private bool jumpConsumed = false;
    private bool interactConsumed = false;

    void Update()
    {
        // === 移动输入 ===
        float moveX = 0f;
        // 有摇杆输入时优先使用摇杆，否则使用键盘输入
        if (joystick != null && Mathf.Abs(joystick.InputDirection.x) > 0.01f)
        {
            moveX = joystick.InputDirection.x;
        }
        else
        {
            moveX = Input.GetAxis("Horizontal");
        }
        GameEvents.FireMoveInput(moveX);

        // === 跳跃输入（单次触发保护） ===
        bool jumpWanted = Input.GetKeyDown(KeyCode.Space);
        // 有跳跃按钮输入时优先使用按钮，否则使用键盘输入
        if (!jumpWanted && jumpButton != null && jumpButton.IsPressed && !jumpConsumed)
        {
            jumpWanted = true;
            jumpConsumed = true;
        }
        if (jumpButton != null && !jumpButton.IsPressed)
        {
            jumpConsumed = false;
        }
        if (jumpWanted)
        {
            GameEvents.FireJumpPressed();
        }

        // === 交互输入（单次触发保护） ===
        bool interactWanted = Input.GetKeyDown(KeyCode.E);
        if (!interactWanted && interactButton != null && interactButton.IsPressed && !interactConsumed)
        {
            interactWanted = true;
            interactConsumed = true;
        }
        if (interactButton != null && !interactButton.IsPressed)
        {
            interactConsumed = false;
        }
        if (interactWanted)
        {
            GameEvents.FireInteractPressed();
        }
    }
}
