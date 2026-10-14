using UnityEngine;

/// <summary>
/// 场景可点击按钮 - 挂载到谜题按钮物体上（需要 Collider2D）
/// 点击后通过 GameEvents 触发弹窗显示
/// </summary>
public class SceneItemButton : MonoBehaviour
{
    public ItemData itemData;

    void OnMouseDown()
    {
        Debug.Log($"[SceneItemButton] 点击了: {itemData?.itemName ?? "itemData为空"}");
        if (itemData != null)
        {
            GameEvents.FireItemPopupOpen(itemData);
        }
    }
}
