#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.Tests
{
    public sealed class ReviewV2SchemeVisualTests
    {
        [UnityTest] public IEnumerator CaptureBothAuthoredLayoutsForVisualInspection()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            while (!scene.isLoaded) yield return null;
            var host = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewSessionHost>(true)).Single();
            var camera = new GameObject("M2 visual inspection camera").AddComponent<Camera>();
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            target.Create(); camera.targetTexture = target;
            try
            {
                host.locomotion.Origin.gameObject.SetActive(false);
                host.locomotion.Origin.Camera.transform.position = new Vector3(-3.15f, 1.65f, 1.12f);
                host.Execute(CommandType.CompleteSetup); host.Execute(CommandType.CompleteOnboarding);
                scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewIntegrationPanel>(true)).Single().SetOpen(false);
                camera.fieldOfView = 70; camera.nearClipPlane = .03f; camera.farClipPlane = 30;
                Directory.CreateDirectory("Logs/ReviewV2/M2/Visual");
                foreach (var scheme in new[] { SchemeIds.IntegratedKitchen, SchemeIds.ExternalRefrigerator })
                {
                    if (host.Snapshot.SchemeId != scheme) host.Execute(CommandType.ChangeScheme, scheme);
                    yield return ReviewOperationWait.Idle(host);
                    Assert.AreEqual(scheme, host.Snapshot.SchemeId);
                    camera.transform.position = scheme == SchemeIds.IntegratedKitchen ? new Vector3(-3.5f, 1.65f, 1.15f) : new Vector3(-3.3f, 1.65f, .05f);
                    camera.transform.LookAt(host.Resolve("FRIDGE").position + Vector3.up * .95f);
                    for (int i = 0; i < 20; i++) yield return null;
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes("Logs/ReviewV2/M2/Visual/" + scheme + ".png", pixels.EncodeToPNG());
                    Object.Destroy(pixels);
                    Assert.IsTrue(File.Exists("Logs/ReviewV2/M2/Visual/" + scheme + ".png"));
                }
            }
            finally { camera.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(camera.gameObject); }
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }
    }
}
#endif
