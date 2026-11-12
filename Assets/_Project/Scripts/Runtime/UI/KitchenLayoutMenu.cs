using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XRApartment.Runtime.UI
{
    public sealed class KitchenLayoutMenu : MonoBehaviour
    {
        [SerializeField] private KitchenLayoutLab lab;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text observedSwing;
        [SerializeField] private TMP_Text selectedFinish;
        [SerializeField] private Button[] presets;
        [SerializeField] private Button[] finishes = System.Array.Empty<Button>();
        [SerializeField] private Button standard, compact;
        [SerializeField] private Button close;
        private LocomotionSettingsView owner;

        public void Configure(KitchenLayoutLab layout, TMP_Text detail, TMP_Text measured, Button[] options,
            Button standardSize, Button compactSize, TMP_Text appearance, Button[] finishOptions, Button done)
        {
            lab = layout;
            status = detail;
            observedSwing = measured;
            presets = options;
            standard = standardSize;
            compact = compactSize;
            selectedFinish = appearance;
            finishes = finishOptions;
            close = done;
        }

        private void Awake()
        {
            owner = GetComponentInParent<LocomotionSettingsView>();
            close.onClick.AddListener(owner.CloseLayout);
            standard.onClick.AddListener(() => ChooseFridge(false));
            compact.onClick.AddListener(() => ChooseFridge(true));
            for (var index = 0; index < presets.Length; index++)
            {
                var option = index;
                presets[index].onClick.AddListener(() => Choose(option));
            }
            for (var index = 0; index < finishes.Length; index++)
            {
                var option = index;
                finishes[index].onClick.AddListener(() => ChooseFinish(option));
            }
        }

        private void OnEnable()
        {
            KitchenLocale.Set(close.GetComponentInChildren<TMP_Text>(), owner.ReturnsToPauseFromLayout ? "Back" : "CLOSE");
            Refresh(lab.Description(lab.ActivePresetIndex));
        }

        private void Choose(int option)
        {
            lab.TryApply(option, out var message);
            Refresh(message);
        }

        private void ChooseFridge(bool compactSize)
        {
            lab.TrySelectFridge(compactSize, out var message);
            Refresh(message);
        }

        private void ChooseFinish(int option)
        {
            if (lab.TrySelectFinish(option)) RefreshFinish();
        }

        private void Refresh(string message)
        {
            KitchenLocale.Set(status, lab.Label(lab.ActivePresetIndex) + "\n" + message);
            KitchenLocale.Set(observedSwing, "Door swing   |   Full " + lab.ObservedSwing(false) + "   ·   Compact " + lab.ObservedSwing(true));
            standard.interactable = lab.CompactSelected;
            compact.interactable = !lab.CompactSelected;
            for (var index = 0; index < presets.Length; index++)
                presets[index].interactable = index != lab.ActivePresetIndex;
            RefreshFinish();
        }

        private void RefreshFinish()
        {
            if (selectedFinish == null || lab.FinishCount == 0) return;
            KitchenLocale.Set(selectedFinish, "Finish   |   " + lab.FinishLabel(lab.ActiveFinishIndex));
            for (var index = 0; index < finishes.Length; index++)
                finishes[index].interactable = index != lab.ActiveFinishIndex;
        }
    }
}
