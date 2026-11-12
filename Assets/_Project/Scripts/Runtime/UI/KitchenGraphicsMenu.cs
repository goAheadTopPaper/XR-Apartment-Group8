using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace XRApartment.Runtime.UI
{
    public sealed class KitchenGraphicsMenu : MonoBehaviour
    {
        public const string Preference = "XRApartment.GraphicsQuality";
        public const int Performance = 0;
        public const int Standard = 1;
        public const int RayTracing = 2;

        [SerializeField] GameObject panel;
        [SerializeField] Button performance, standard, rayTracing, back;
        [SerializeField] TMP_Text status;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Restore()
        {
            var saved = PlayerPrefs.GetInt(Preference, Standard);
            QualitySettings.SetQualityLevel(saved >= Performance && saved <= RayTracing ? saved : Standard, true);
        }

        internal void Bind(KitchenPauseMenu pause)
        {
            performance.onClick.AddListener(() => Select(Performance));
            standard.onClick.AddListener(() => Select(Standard));
            rayTracing.onClick.AddListener(() => Select(RayTracing));
            back.onClick.AddListener(() => pause.Open());
        }

        internal void Open(LocomotionSettingsView menu)
        {
            menu.OpenAuxiliary(panel);
            Refresh();
        }

        void Select(int quality)
        {
            QualitySettings.SetQualityLevel(quality, true);
            PlayerPrefs.SetInt(Preference, quality);
            PlayerPrefs.Save();
            Refresh();
        }

        void LateUpdate()
        {
            if (panel.activeSelf) Refresh();
        }

        void Refresh()
        {
            var level = QualitySettings.GetQualityLevel();
            performance.interactable = level != Performance;
            standard.interactable = level != Standard;
            rayTracing.interactable = level != RayTracing;

            string message;
            switch (level)
            {
                case Performance:
                    message = "Performance selected. Ray tracing off.";
                    break;
                case RayTracing:
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12 || !SystemInfo.supportsRayTracing)
                        message = "High quality. Ray tracing requires a DirectX 12 ray tracing GPU.";
                    else if (RenderPipelineManager.currentPipeline is HDRenderPipeline pipeline && pipeline.rayTracingSupported)
                        message = "High quality. Ray-traced reflections enabled.";
                    else
                        message = "High quality. Preparing ray-traced reflections.";
                    break;
                default:
                    message = "Standard selected. Ray tracing off.";
                    break;
            }
            KitchenLocale.Set(status, message);
        }
    }
}
