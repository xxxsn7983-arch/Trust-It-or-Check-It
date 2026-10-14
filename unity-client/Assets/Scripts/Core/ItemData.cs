using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 场景中可点击物品的数据（在 SceneItemButton 的 Inspector 中配置）
/// </summary>
[System.Serializable]
public class ItemData
{
    public string itemName;             // 物品名称（显示在弹窗标题）
    public Sprite popupImage;           // 物品图片（显示在弹窗中，优先于视频）
    public VideoClip videoClip;         // 物品视频（如果有，则在弹窗中播放，优先于图片）   
    public ClueInfo collectibleClue;    // 可收集的线索信息（如果有，点击收集按钮后触发收集事件）
}

/// <summary>
/// 可收集的线索信息
/// </summary>
[System.Serializable]
public class ClueInfo
{
    public string clueId;           // 线索 ID（唯一标识符，用于事件触发）
    public string clueName;         // 线索名称
    public Sprite clueIcon;         // 线索图标
    public Sprite clueFullImage;    // 线索全图（在查看线索详情时显示，优先于 clueIcon）
}
