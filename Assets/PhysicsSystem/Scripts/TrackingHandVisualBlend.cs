using UnityEngine;

public class TrackingHandVisualBlend : MonoBehaviour
{
    [Header("Hand References")]
    public Transform physicsHandRoot;    // 物理手根节点
    public Transform trackedHandRoot;     // XR追踪手根节点
    public GameObject trackedHandVisual;   // 追踪手渲染物体（SkinnedMeshRenderer所在GameObject）

    [Header("Distance Threshold (Meter)")]
    public float showDistance = 0.08f;    // 超过这个距离，显示追踪手
    public float hideDistance = 0.04f;    // 低于这个距离，隐藏追踪手（迟滞防闪烁）

    private bool isTrackedHandVisible;

    private void Update()
    {
        float distance = Vector3.Distance(physicsHandRoot.position, trackedHandRoot.position);

        if (!isTrackedHandVisible)
        {
            // 当前隐藏，距离超出上限 → 显示
            if (distance > showDistance)
            {
                SetTrackedHandVisible(true);
            }
        }
        else
        {
            // 当前显示，距离回到下限以内 → 隐藏
            if (distance < hideDistance)
            {
                SetTrackedHandVisible(false);
            }
        }
    }

    void SetTrackedHandVisible(bool visible)
    {
        isTrackedHandVisible = visible;
        trackedHandVisual.SetActive(visible);
    }

    // Scene调试Gizmos
    private void OnDrawGizmosSelected()
    {
        if (physicsHandRoot && trackedHandRoot)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(physicsHandRoot.position, trackedHandRoot.position);
            float dist = Vector3.Distance(physicsHandRoot.position, trackedHandRoot.position);
            Debug.Log($"Hand offset: {dist:F3} m");
        }
    }
}
