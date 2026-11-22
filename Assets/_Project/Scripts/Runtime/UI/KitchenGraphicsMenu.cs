using TMPro;
using UnityEngine;
using UnityEngine.NVIDIA;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace XRApartment.Runtime.UI
{
    public sealed class KitchenGraphicsMenu : MonoBehaviour
    {
        public const string Preference = "XRApartment.GraphicsQuality";
        public const string RayTracingPreference = "XRApartment.Graphics.RayTracing";
        public const string DlssPreference = "XRApartment.Graphics.DLSS";
        public const int Performance = 0;
        public const int Standard = 1;
        public const int RayTracing = 2; // Serialized legacy preset index; shown as High.

        [SerializeField] GameObject panel;
        [SerializeField] Button performance, standard, rayTracing, back;
        [SerializeField] TMP_Text status;
        Button rayToggle, dlssToggle;
        Camera viewCamera;
        bool hasStatus;
        (int level, bool ray, bool dlss, bool dxr, bool rayReady, bool dlssReady, KitchenLocale.Language language) lastStatus;
        static bool rayRequested, dlssRequested;
        static HDRenderPipelineAsset runtimeAsset, presetAsset;
        static int runtimeLevel;

        static bool DxrAvailable => SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 && SystemInfo.supportsRayTracing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Restore()
        {
            ReleaseRuntimeAsset();
            var saved = PlayerPrefs.GetInt(Preference, Standard);
            var level = saved >= Performance && saved <= RayTracing ? saved : Standard;
            rayRequested = PlayerPrefs.GetInt(RayTracingPreference, level == RayTracing ? 1 : 0) != 0;
            dlssRequested = PlayerPrefs.GetInt(DlssPreference, 0) != 0;
            Apply(level);
            SceneManager.sceneLoaded -= CameraSceneLoaded;
            SceneManager.sceneLoaded += CameraSceneLoaded;
            Application.quitting -= Shutdown;
            Application.quitting += Shutdown;
        }

        static void CameraSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<Unity.XR.CoreUtils.XROrigin>(out var rig) && rig.isActiveAndEnabled)
                    ApplyCamera(rig.Camera);
            }
        }

        static void Shutdown()
        {
            SceneManager.sceneLoaded -= CameraSceneLoaded;
            ReleaseRuntimeAsset();
        }

        static void ReleaseRuntimeAsset()
        {
            if (runtimeAsset == null) return;
            if (QualitySettings.GetQualityLevel() == runtimeLevel && QualitySettings.renderPipeline == runtimeAsset)
                QualitySettings.renderPipeline = presetAsset;
            Destroy(runtimeAsset);
            runtimeAsset = presetAsset = null;
        }

        static void Apply(int level)
        {
            ReleaseRuntimeAsset();
            if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
            if (QualitySettings.renderPipeline is HDRenderPipelineAsset source)
            {
                var original = source.currentPlatformRenderPipelineSettings;
                var configured = Configure(original, rayRequested && DxrAvailable, dlssRequested);
                if (configured.supportRayTracing != original.supportRayTracing || dlssRequested)
                {
                    // A runtime copy avoids writing settings back into the project quality assets.
                    presetAsset = source;
                    runtimeLevel = level;
                    runtimeAsset = Instantiate(source);
                    runtimeAsset.name = source.name + " (runtime options)";
                    runtimeAsset.hideFlags = HideFlags.HideAndDontSave;
                    runtimeAsset.currentPlatformRenderPipelineSettings = configured;
                    QualitySettings.renderPipeline = runtimeAsset;
                }
            }
            ApplyCamera(Camera.main);
        }

        public static RenderPipelineSettings Configure(RenderPipelineSettings preset, bool ray, bool dlss)
        {
            preset.supportRayTracing = ray;
            if (ray) preset.supportSSR = true; // HDRP uses the SSR pass to dispatch opaque reflections.
            if (!dlss) return preset;
            var dynamicResolution = preset.dynamicResolutionSettings;
            dynamicResolution.enabled = true;
            dynamicResolution.enableDLSS = true;
            dynamicResolution.DLSSPerfQualitySetting = (uint)DLSSQuality.MaximumQuality;
            dynamicResolution.DLSSUseOptimalSettings = true;
            // If NVIDIA isn't detected, leave the native backbuffer size. HDRP supplies the
            // recommended scale and replaces these bounds when it creates a working DLSS pass.
            dynamicResolution.minPercentage = dynamicResolution.maxPercentage = 100f;
            dynamicResolution.forceResolution = false;
            preset.dynamicResolutionSettings = dynamicResolution;
            return preset;
        }

        static void ApplyCamera(Camera camera)
        {
            if (camera == null || !camera.TryGetComponent(out HDAdditionalCameraData data)) return;
            camera.allowDynamicResolution = dlssRequested;
            data.allowDynamicResolution = dlssRequested;
            data.allowDeepLearningSuperSampling = dlssRequested;
            data.deepLearningSuperSamplingUseCustomQualitySettings = false;
            data.deepLearningSuperSamplingUseCustomAttributes = false;
            data.customRenderingSettings = true;
            SetFrameOption(data, FrameSettingsField.RayTracing, rayRequested && DxrAvailable);
            if (QualitySettings.renderPipeline is HDRenderPipelineAsset asset)
                SetFrameOption(data, FrameSettingsField.SSR, asset.currentPlatformRenderPipelineSettings.supportSSR);
        }

        static void SetFrameOption(HDAdditionalCameraData camera, FrameSettingsField field, bool enabled)
        {
            camera.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
            camera.renderingPathCustomFrameSettings.SetEnabled(field, enabled);
        }

        internal void Bind(KitchenPauseMenu pause)
        {
            performance.onClick.AddListener(() => Select(Performance));
            standard.onClick.AddListener(() => Select(Standard));
            rayTracing.onClick.AddListener(() => Select(RayTracing));
            back.onClick.AddListener(() => pause.Open());
            rayToggle = AddToggle("RayTracingToggle", -166f, () => { rayRequested = !rayRequested; Select(QualitySettings.GetQualityLevel()); });
            dlssToggle = AddToggle("DLSSToggle", 166f, () => { dlssRequested = !dlssRequested; Select(QualitySettings.GetQualityLevel()); });
            var rect = status.rectTransform;
            rect.anchoredPosition = new Vector2(0f, -142f);
            rect.sizeDelta = new Vector2(666f, 57f);
            status.fontSize = 20;
            ((RectTransform)back.transform).anchoredPosition = new Vector2(233f, -201f);
        }

        Button AddToggle(string name, float x, UnityEngine.Events.UnityAction clicked)
        {
            var button = Instantiate(performance, panel.transform);
            button.name = name;
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(x, -88f);
            rect.sizeDelta = new Vector2(318f, 46f);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.fontSize = 23;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(clicked);
            return button;
        }

        internal void Open(LocomotionSettingsView menu)
        {
            menu.OpenAuxiliary(panel);
            viewCamera = GetComponentInParent<KitchenLocomotionSettings>()?.Origin?.Camera;
            ApplyCamera(viewCamera);
            hasStatus = false;
            Refresh();
        }

        void Select(int quality)
        {
            Apply(quality);
            ApplyCamera(viewCamera);
            PlayerPrefs.SetInt(Preference, quality);
            PlayerPrefs.SetInt(RayTracingPreference, rayRequested ? 1 : 0);
            PlayerPrefs.SetInt(DlssPreference, dlssRequested ? 1 : 0);
            PlayerPrefs.Save();
            Refresh();
        }

        void LateUpdate()
        {
            if (panel.activeSelf && rayToggle != null) Refresh();
        }

        void Refresh()
        {
            var level = QualitySettings.GetQualityLevel();
            var pipeline = RenderPipelineManager.currentPipeline as HDRenderPipeline;
            var state = (level, rayRequested, dlssRequested, DxrAvailable, pipeline?.rayTracingSupported == true,
                pipeline != null && HDDynamicResolutionPlatformCapabilities.DLSSDetected, KitchenLocale.Current);
            if (hasStatus && state.Equals(lastStatus)) return;
            hasStatus = true;
            lastStatus = state;
            performance.interactable = level != Performance;
            standard.interactable = level != Standard;
            rayTracing.interactable = level != RayTracing;
            SetLabel(rayTracing, T("High", "高画质", "高畫質"));
            SetLabel(rayToggle, T("Ray tracing: ", "光追：", "光追：") + OnOff(rayRequested));
            SetLabel(dlssToggle, "DLSS: " + OnOff(dlssRequested));
            var rayStatus = !rayRequested ? T("Off (raster reflections)", "关闭（光栅反射）", "關閉（光柵反射）") :
                !DxrAvailable ? T("Unavailable: requires D3D12 and DXR", "不可用：需要 D3D12 和 DXR", "不可用：需要 D3D12 和 DXR") :
                state.Item5 ? T("On (reflections)", "已启用（反射）", "已啟用（反射）") : T("Waiting for HDRP / DXR", "等待 HDRP / DXR", "等待 HDRP / DXR");
            var dlssStatus = !dlssRequested ? T("Off (native resolution)", "关闭（原生分辨率）", "關閉（原生解析度）") :
                state.Item6 ? T("On (Quality)", "已启用（质量档）", "已啟用（品質檔）") :
                T("Unavailable: NVIDIA DLSS not detected; native", "未检测到 NVIDIA DLSS；用原生分辨率", "未偵測到 NVIDIA DLSS；用原生解析度");
            KitchenLocale.SetVerbatim(status, T("Ray tracing: ", "光追：", "光追：") + rayStatus + "\nDLSS: " + dlssStatus);
        }

        static void SetLabel(Button button, string value) => KitchenLocale.SetVerbatim(button.GetComponentInChildren<TMP_Text>(true), value);
        static string OnOff(bool on) => on ? T("On", "开", "開") : T("Off", "关", "關");
        static string T(string en, string zh, string tw) => KitchenLocale.Current == KitchenLocale.Language.SimplifiedChinese ? zh :
            KitchenLocale.Current == KitchenLocale.Language.TraditionalChinese ? tw : en;
    }
}
