using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XRApartment.Runtime.Storage;

namespace XRApartment.Runtime.UI
{
    public sealed class KitchenFeedbackNotes : MonoBehaviour
    {
        [SerializeField] LocomotionSettingsView menu;
        [SerializeField] KitchenLocomotionSettings movement;
        [SerializeField] KitchenLayoutLab layout;
        [SerializeField] KitchenFeedbackNote notePrefab;
        [SerializeField] RectTransform editor;
        [SerializeField] CanvasGroup form;
        [SerializeField] Button close, discard, save, writeInVr;
        [SerializeField] TMP_InputField input;
        [SerializeField] TMP_Text target, context, status;
        [SerializeField] Shader keyboardSurfaceShader, keyboardTextShader;
        KitchenRayKeyboard keyboard;
        KitchenFeedbackNote draft;
        RectTransform savedPanel;
        Button openSaved, copySaved;
        TMP_Text savedInfo;
        string objectId, objectName, layoutName, fridgeName;
        float spawnErrorUntil;
        bool editorWasOpen;

        internal event Action Saved;
        internal bool HasLooseNote => draft != null && !draft.IsPinned;
        internal bool SpawnBlocked => Time.unscaledTime < spawnErrorUntil;
        public static string DocumentPath => Path.Combine(Application.persistentDataPath, "KitchenFeedback", "feedback.txt");

        void Awake()
        {
            close.onClick.AddListener(Close);
            discard.onClick.AddListener(Discard);
            save.onClick.AddListener(Save);
            writeInVr.onClick.AddListener(OpenKeyboard);
            input.onValueChanged.AddListener(_ => RefreshSave());
            ArrangeDockedEditor();
        }

        void ArrangeDockedEditor()
        {
            // A stable upper form, with an independent panel below it. Hiding the
            // keyboard never toggles off the form or moves the text field.
            editor.sizeDelta = new Vector2(880f, 750f);
            var region = (RectTransform)form.transform;
            region.sizeDelta = new Vector2(880f, 370f);
            region.anchoredPosition = new Vector2(0f, 186f);
            var background = editor.GetComponent<Image>();
            var upperSurface = region.gameObject.AddComponent<Image>();
            upperSurface.material = background.material;
            upperSurface.color = background.color;
            upperSurface.raycastTarget = false;
            upperSurface.transform.SetAsFirstSibling();
            background.enabled = false;
            Place((RectTransform)region.Find("Title"), 151f, 42f);
            Place(target.rectTransform, 108f, 36f);
            Place(context.rectTransform, 73f, 30f);
            Place((RectTransform)input.transform, 1f, 102f);
            Place(input.textViewport, 0f, 86f);
            Place(input.textComponent.rectTransform, 0f, 82f);
            Place((RectTransform)input.placeholder.transform, 0f, 82f);
            Place(status.rectTransform, -66f, 26f);
            Place((RectTransform)writeInVr.transform, -105f, 43f);
            Place((RectTransform)save.transform, -105f, 43f);
            Place((RectTransform)discard.transform, -154f, 42f);
            Place((RectTransform)close.transform, -154f, 42f);

            static void Place(RectTransform rect, float y, float height)
            {
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            }
        }

        void OnDestroy() => keyboard?.ReleaseMaterials();

        void LateUpdate()
        {
            // U / Menu and opening another panel use the shared menu owner,
            // so they can dismiss this view without going through our Close button.
            if (!editorWasOpen || editor.gameObject.activeInHierarchy) return;
            editorWasOpen = false;
            CloseKeyboard();
            input.DeactivateInputField();
            RemoveBlankDraft();
        }

        internal void OpenOrSpawn()
        {
            if (draft != null && draft.IsPinned)
            {
                OpenEditor();
                return;
            }

            menu.Close();
            var creating = draft == null;
            if (creating)
            {
                draft = Instantiate(notePrefab);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(draft.gameObject, gameObject.scene);
                draft.Pinned += OnPinned;
                keyboard?.ResetComposition();
                input.text = "";
            }
            if (draft.PlaceInView(movement.Origin.Camera.transform))
            {
                spawnErrorUntil = 0f;
                return;
            }
            spawnErrorUntil = Time.unscaledTime + 4f;
            if (creating) RemoveDraft();
        }

        void OnPinned(KitchenFeedbackTarget item)
        {
            objectId = item.ObjectId;
            objectName = item.DisplayName;
            layoutName = layout.Label(layout.ActivePresetIndex);
            fridgeName = layout.CompactSelected ? "Compact fridge" : "Full-size fridge";
            OpenEditor();
        }

        void OpenEditor()
        {
            input.interactable = discard.interactable = writeInVr.interactable = true;
            KitchenLocale.Set(target, objectName);
            KitchenLocale.Set(context, layoutName + " | " + fridgeName);
            KitchenLocale.Set(status, "");
            RefreshSave();
            menu.OpenAuxiliary(editor.gameObject);
            editorWasOpen = true;
            OpenKeyboard();
        }

        void OpenKeyboard()
        {
            if (keyboard == null)
                keyboard = new KitchenRayKeyboard("FeedbackKeyboard", editor, editor, input, "Write your feedback",
                    "", HideDock, keyboardSurfaceShader, keyboardTextShader, OpenNativeInput, HideDock);
            keyboard.Show(KitchenLocale.Chinese);
        }

        void HideDock()
        {
            // The composition can continue when the dock is reopened. The draft and
            // explicit form actions stay in view even when the dock is hidden.
            CloseKeyboard();
        }

        void OpenNativeInput()
        {
            if (keyboard != null && keyboard.HasComposition)
            {
                KitchenLocale.Set(status, "Choose a pinyin candidate before changing input.");
                return;
            }
            CloseKeyboard();
            EventSystem.current?.SetSelectedGameObject(input.gameObject);
            input.ActivateInputField();
        }

        void CloseKeyboard() => keyboard?.Hide();

        void Close()
        {
            CloseKeyboard();
            input.DeactivateInputField();
            RemoveBlankDraft();
            menu.Close();
            editorWasOpen = false;
        }

        void RemoveBlankDraft()
        {
            if (draft != null && draft.IsPinned && string.IsNullOrWhiteSpace(input.text)) RemoveDraft();
        }

        void RemoveDraft()
        {
            if (draft != null) Destroy(draft.gameObject);
            draft = null;
            keyboard?.ResetComposition();
            input.text = "";
        }

        void Discard()
        {
            RemoveDraft();
            Close();
        }

        void RefreshSave() => save.interactable = draft != null && draft.IsPinned && !string.IsNullOrWhiteSpace(input.text);

        void Save()
        {
            if (!save.interactable || draft == null) return;
            if (keyboard != null && keyboard.HasComposition)
            {
                OpenKeyboard();
                KitchenLocale.Set(status, "Choose a pinyin candidate before saving.");
                return;
            }
            try
            {
                KitchenFeedbackDocument.Append(DocumentPath, objectId, objectName, layoutName, fridgeName, input.text.Trim());
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning("Kitchen feedback could not be saved: " + error.Message, this);
                KitchenLocale.Set(status, "Could not save. Your draft is still here. Try again.");
                return;
            }

            draft.MarkSaved();
            draft = null;
            input.DeactivateInputField();
            input.interactable = save.interactable = discard.interactable = writeInVr.interactable = false;
            KitchenLocale.Set(status, "Saved to KitchenFeedback/feedback.txt");
            Saved?.Invoke();
        }

        internal void OpenSavedFeedback(Action back)
        {
            if (savedPanel == null)
            {
                savedPanel = KitchenRayKeyboard.Rect("SavedFeedback", editor.parent, 0f, 0f, 780f, 400f);
                savedPanel.localScale = editor.localScale;
                savedPanel.gameObject.SetActive(false);
                var surface = editor.GetComponent<Image>();
                var background = savedPanel.gameObject.AddComponent<Image>();
                background.material = surface.material;
                background.color = surface.color;
                var sample = status;
                Text("Title", 0, 144, 700, 48, "Saved feedback", 34);
                Text("Help", 0, 82, 700, 55, "Open the file location or copy its path to share the file yourself.", 24);
                savedInfo = Text("Info", 0, -5, 698, 82, "", 22);
                openSaved = Button("OpenFolder", -176, -88, 323, "Open feedback folder", OpenSavedFolder);
                copySaved = Button("CopyPath", 176, -88, 323, "Copy feedback path", CopySavedPath);
                Button("Back", 0, -155, 246, "Back", () => back());

                TMP_Text Text(string name, float x, float y, float width, float height, string value, int size)
                {
                    var text = KitchenRayKeyboard.Text("Text", KitchenRayKeyboard.Rect(name, savedPanel, x, y, width, height),
                        value, size, sample.font);
                    text.fontSharedMaterial = sample.fontSharedMaterial;
                    return text;
                }
                Button Button(string name, float x, float y, float width, string label, UnityEngine.Events.UnityAction action)
                {
                    var button = KitchenRayKeyboard.Button(KitchenRayKeyboard.Rect(name, savedPanel, x, y, width, 50f),
                        label, sample.font, action);
                    button.image.material = surface.material;
                    button.GetComponentInChildren<TMP_Text>().fontSharedMaterial = sample.fontSharedMaterial;
                    return button;
                }
            }
            var available = File.Exists(DocumentPath);
            copySaved.interactable = available;
            openSaved.interactable = available && (Application.platform == RuntimePlatform.WindowsPlayer ||
                Application.platform == RuntimePlatform.WindowsEditor);
            KitchenLocale.Set(savedInfo, available ? "Saved to KitchenFeedback/feedback.txt" : "No saved feedback yet.");
            menu.OpenAuxiliary(savedPanel.gameObject);
        }

        void CopySavedPath()
        {
            if (!File.Exists(DocumentPath)) { OpenMissing(); return; }
            GUIUtility.systemCopyBuffer = DocumentPath;
            KitchenLocale.Set(savedInfo, "Feedback path copied.");
        }

        void OpenSavedFolder()
        {
            if (!File.Exists(DocumentPath)) { OpenMissing(); return; }
            try
            {
                using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Path.GetDirectoryName(DocumentPath),
                    UseShellExecute = true
                })) { }
            }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception || error is InvalidOperationException)
            {
                KitchenLocale.Set(savedInfo, "Could not open the folder. Copy the feedback path instead.");
            }
        }

        void OpenMissing()
        {
            openSaved.interactable = copySaved.interactable = false;
            KitchenLocale.Set(savedInfo, "No saved feedback yet.");
        }
    }
}
