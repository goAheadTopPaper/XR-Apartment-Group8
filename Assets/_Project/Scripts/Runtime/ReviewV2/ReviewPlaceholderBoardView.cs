using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XRApartment.Runtime.ReviewV2
{
    // Local display states, not a T3 data schema or a repository contract.
    public enum ReviewBoardDisplayState { NotConnected, Loading, Empty, Ready, Failed }

    public sealed class ReviewPlaceholderBoardView : MonoBehaviour
    {
        [Tooltip("Optional T3 component. A reference alone does not enable data access.")]
        public MonoBehaviour t3Source;
        public Button currentScheme, allSchemes;
        public TMP_Text listText, detailText;
        public ScrollRect listScroll, detailScroll;
        public ReviewBoardDisplayState State { get; private set; }

        public void ConfigurePresentation()
        {
            foreach (var filter in new[] { currentScheme, allSchemes })
            {
                ((RectTransform)filter.transform).sizeDelta = new Vector2(206, 52);
                var label = filter.GetComponentInChildren<TMP_Text>(true);
                label.fontSize = 28; label.rectTransform.sizeDelta = new Vector2(186, 44);
            }
            foreach (var text in new[] { listText, detailText })
            {
                text.fontSize = 30; text.alignment = TextAlignmentOptions.TopLeft;
                text.overflowMode = TextOverflowModes.Overflow; text.richText = false;
            }
        }
        public void ShowUnavailable()
        {
            Present(ReviewBoardDisplayState.NotConnected, t3Source == null ? "T3 未接入\n暂不能读取标记" : "T3 组件已配置\n待完成接口适配",
                "标记详情\n\n接入 T3 后，可在这里查看标记的方案、位置和备注。\n当前没有加载反馈数据。");
        }
        // Presentation only. The real T3 adapter will supply actual read results later.
        public void Present(ReviewBoardDisplayState state, string list = null, string detail = null)
        {
            State = state;
            string fallback;
            switch (state)
            {
                case ReviewBoardDisplayState.Loading: fallback = "正在读取标记…"; break;
                case ReviewBoardDisplayState.Empty: fallback = "暂无标记"; break;
                case ReviewBoardDisplayState.Ready: fallback = "标记已加载"; break;
                case ReviewBoardDisplayState.Failed: fallback = "标记读取失败，请重试"; break;
                default: fallback = "T3 未接入，暂不能读取标记"; break;
            }
            listText.text = list ?? fallback; detailText.text = detail ?? "标记详情";
            // Filters remain unavailable until actual repository queries are adapted.
            currentScheme.interactable = allSchemes.interactable = false;
            Canvas.ForceUpdateCanvases();
            listScroll.verticalNormalizedPosition = detailScroll.verticalNormalizedPosition = 1;
        }
    }
}
