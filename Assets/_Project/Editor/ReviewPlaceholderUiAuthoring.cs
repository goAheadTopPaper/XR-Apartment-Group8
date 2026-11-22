using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.EditorTools
{
    // Incremental authoring of the temporary UI; existing Prefab objects stay in place.
    public static class ReviewPlaceholderUiAuthoring
    {
        const string FontPath = "Assets/_Project/Art/UI/FNT_ReviewV2Integration.asset";
        static readonly Color Surface = new Color(.045f, .085f, .12f, 1);
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        static TMP_Text Text(string name, Transform parent, string content, Vector2 size, Vector2 position, int fontSize = 30)
        {
            var text = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            text.text = content; text.fontSize = fontSize; text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft; text.raycastTarget = false;
            return text;
        }
        static UnityEngine.UI.Image Background(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; return image;
        }
        static UnityEngine.UI.Button Button(string name, Transform parent, string label, Vector2 size, Vector2 position)
        {
            var rect = Rect(name, parent, size, position);
            var image = Background(rect, new Color(.10f, .20f, .28f, 1));
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.disabledColor = new Color(.4f, .4f, .4f); button.colors = colors;
            var text = Text("Label", rect, label, size - new Vector2(20, 8), new Vector2(0, -4), 32);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }
        public static void Ensure(ReviewIntegrationPanel view)
        {
            if (view.mark == null)
            {
                view.mark = Object.Instantiate(view.review, view.panel.transform);
                view.mark.name = "Open Mark";
            }
            if (view.markDraft == null) BuildMark(view);
            if (view.reviewBoard == null) BuildBoard(view);
            if (view.quickBar == null) BuildQuickBar(view);
            if (view.brief == null)
                view.brief = Button("Open activity brief", view.panel.transform, "任务书 / Activities", new Vector2(430, 68), Vector2.zero);
            if (view.briefBack == null)
                view.briefBack = Button("Return from activity brief", view.panel.transform, "返回体验 / Back", new Vector2(430, 68), Vector2.zero);
            if (view.quickBrief == null)
                view.quickBrief = Button("Quick activity brief", view.quickBar.transform, "任务书 / Brief", new Vector2(206, 64), new Vector2(-339, -54));
            if (view.activityBrief == null) BuildBrief(view);
        }
        static void BuildBrief(ReviewIntegrationPanel view)
        {
            var root = Rect("Activity brief", view.panel.transform, new Vector2(884, 428), new Vector2(0, -156));
            var brief = root.gameObject.AddComponent<ReviewActivityBriefView>(); view.activityBrief = brief;
            brief.introduction = Text("Introduction", root, "", new Vector2(884, 132), Vector2.zero, 28);
            brief.goalTitles = new TMP_Text[5]; brief.goalStates = new TMP_Text[5];
            for (int i = 0; i < 5; i++)
            {
                var row = Rect("G0" + (i + 1), root, new Vector2(884, 52), new Vector2(0, -148 - i * 56));
                Background(row, Surface).raycastTarget = false;
                brief.goalTitles[i] = Text("Title", row, "", new Vector2(664, 44), new Vector2(-98, -4), 28);
                brief.goalStates[i] = Text("State", row, "未接入", new Vector2(170, 44), new Vector2(345, -4), 28);
            }
            root.gameObject.SetActive(false);
        }
        static void BuildMark(ReviewIntegrationPanel view)
        {
            var root = Rect("Mark editor", view.panel.transform, new Vector2(884, 480), new Vector2(0, -156));
            var mark = root.gameObject.AddComponent<ReviewMarkDraftView>(); view.markDraft = mark;
            var editor = Rect("Editor", root, root.sizeDelta, Vector2.zero);
            mark.editor = editor.gameObject.AddComponent<CanvasGroup>();
            mark.availability = Text("Availability", editor, "标记服务未接入\n界面草稿，未提交、未保存", new Vector2(884, 92), Vector2.zero, 28);
            Text("Note heading", editor, "备注（可试填，最多 2000 字）", new Vector2(884, 48), new Vector2(0, -100));
            var input = Rect("Note input", editor, new Vector2(884, 230), new Vector2(0, -156));
            var image = Background(input, Surface);
            mark.note = input.gameObject.AddComponent<TMP_InputField>(); mark.note.targetGraphic = image;
            var viewport = Rect("Text Area", input, new Vector2(852, 206), new Vector2(0, -12));
            viewport.gameObject.AddComponent<RectMask2D>();
            var note = Text("Text", viewport, "", viewport.sizeDelta, Vector2.zero);
            var placeholder = Text("Placeholder", viewport, "在这里试填厨房体验中的问题或建议…\nT3 接入前不会提交或保存。", viewport.sizeDelta, Vector2.zero);
            placeholder.color = new Color(.62f, .71f, .78f);
            mark.note.textViewport = viewport;
            mark.note.textComponent = (TextMeshProUGUI)note; mark.note.placeholder = placeholder;
            mark.capture = Button("Capture position", editor, "位置待接入", new Vector2(280, 68), new Vector2(-300, -406));
            mark.submit = Button("Submit marker", editor, "提交待接入", new Vector2(280, 68), new Vector2(0, -406));
            mark.cancel = Button("Cancel draft", editor, "取消 / 返回", new Vector2(280, 68), new Vector2(300, -406));
            var prompt = Rect("Discard confirmation", root, root.sizeDelta, Vector2.zero);
            Background(prompt, new Color(.025f, .045f, .065f, 1)); mark.discardPrompt = prompt.gameObject;
            mark.discardMessage = Text("Message", prompt, "丢弃未提交的草稿？\n\n试填的备注尚未提交，也没有保存。", new Vector2(804, 180), new Vector2(0, -60), 32);
            mark.discard = Button("Discard draft", prompt, "丢弃并返回", new Vector2(420, 68), new Vector2(-222, -285));
            mark.keepEditing = Button("Keep editing", prompt, "继续填写", new Vector2(420, 68), new Vector2(222, -285));
            prompt.gameObject.SetActive(false); root.gameObject.SetActive(false);
        }
        static void BuildBoard(ReviewIntegrationPanel view)
        {
            var root = Rect("Review board", view.panel.transform, new Vector2(884, 246), new Vector2(0, -270));
            var board = root.gameObject.AddComponent<ReviewPlaceholderBoardView>(); view.reviewBoard = board;
            board.currentScheme = Button("Current scheme filter", root, "当前方案", new Vector2(206, 52), new Vector2(-339, 0));
            board.allSchemes = Button("All schemes filter", root, "全部方案", new Vector2(206, 52), new Vector2(-115, 0));
            board.listScroll = Scroll("Marker list", root, new Vector2(430, 190), new Vector2(-227, -56), out var list);
            board.detailScroll = Scroll("Marker detail", root, new Vector2(430, 246), new Vector2(227, 0), out var detail);
            board.listText = list; board.detailText = detail;
            board.ShowUnavailable(); root.gameObject.SetActive(false);
        }
        static ScrollRect Scroll(string name, Transform parent, Vector2 size, Vector2 position, out TMP_Text text)
        {
            var root = Rect(name, parent, size, position); Background(root, Surface);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            var viewport = Rect("Viewport", root, size - new Vector2(26, 8), new Vector2(-9, -4));
            Background(viewport, Color.white);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport, new Vector2(viewport.sizeDelta.x, 0), Vector2.zero);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            text = Text("Text", content, "T3 未接入", new Vector2(viewport.sizeDelta.x - 16, 100), Vector2.zero);
            text.overflowMode = TextOverflowModes.Overflow;
            scroll.viewport = viewport; scroll.content = content;
            var bar = Rect("Scrollbar", root, new Vector2(12, size.y - 8), new Vector2(size.x * .5f - 10, -4));
            Background(bar, new Color(.12f, .2f, .25f));
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            var handle = Rect("Handle", bar, new Vector2(12, size.y - 8), Vector2.zero);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one;
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            scrollbar.targetGraphic = Background(handle, new Color(.46f, .64f, .72f));
            scrollbar.handleRect = handle; scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            return scroll;
        }
        static void BuildQuickBar(ReviewIntegrationPanel view)
        {
            var root = Rect("Experience quick actions", view.transform, new Vector2(960, 132), Vector2.zero);
            // A world-space HUD positioned under the view at runtime, owned by the session UI.
            root.pivot = new Vector2(.5f, .5f); root.localScale = Vector3.one * .001f;
            Background(root, new Color(.025f, .045f, .065f, 1));
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 260;
            root.gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 20;
            var raycaster = root.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            raycaster.checkFor2DOcclusion = raycaster.checkFor3DOcclusion = false;
            view.quickBar = root.gameObject;
            view.quickStatus = Text("Status", root, "体验快捷入口", new Vector2(884, 44), new Vector2(0, -8), 28);
            view.quickMark = Button("Quick Mark", root, "记录 / Mark", new Vector2(280, 64), new Vector2(-300, -54));
            view.quickReview = Button("Quick Review", root, "回顾 / Review", new Vector2(280, 64), new Vector2(0, -54));
            view.quickMenu = Button("Quick menu", root, "菜单 / F8", new Vector2(280, 64), new Vector2(300, -54));
            root.gameObject.SetActive(false);
        }
    }
}
