using System;
using System.IO;
using TMPro;
using UnityEngine;
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
        string objectId, objectName, layoutName, fridgeName;
        float spawnErrorUntil;

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
        }

        void OnDestroy() => keyboard?.ReleaseMaterials();

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
                input.text = "";
            }
            if (draft.PlaceInView(movement.Origin.Camera.transform))
            {
                spawnErrorUntil = 0f;
                return;
            }
            spawnErrorUntil = Time.unscaledTime + 4f;
            if (creating)
            {
                Destroy(draft.gameObject);
                draft = null;
            }
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
            CloseKeyboard();
            input.interactable = discard.interactable = writeInVr.interactable = true;
            KitchenLocale.Set(target, objectName);
            KitchenLocale.Set(context, layoutName + " | " + fridgeName);
            KitchenLocale.Set(status, "");
            RefreshSave();
            menu.OpenAuxiliary(editor.gameObject);
        }

        void OpenKeyboard()
        {
            if (keyboard == null)
                keyboard = new KitchenRayKeyboard("FeedbackKeyboard", editor, editor, input, "Write your feedback",
                    "Use the controller ray and trigger. Done returns to your note.", CloseKeyboard,
                    keyboardSurfaceShader, keyboardTextShader);
            form.alpha = 0f;
            form.interactable = form.blocksRaycasts = false;
            keyboard.Show();
        }

        void CloseKeyboard()
        {
            keyboard?.Hide();
            form.alpha = 1f;
            form.interactable = form.blocksRaycasts = true;
        }

        void Close()
        {
            CloseKeyboard();
            input.DeactivateInputField();
            menu.Close();
        }

        void Discard()
        {
            if (draft != null) Destroy(draft.gameObject);
            draft = null;
            input.text = "";
            Close();
        }

        void RefreshSave() => save.interactable = draft != null && draft.IsPinned && !string.IsNullOrWhiteSpace(input.text);

        void Save()
        {
            if (!save.interactable || draft == null) return;
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
    }
}
