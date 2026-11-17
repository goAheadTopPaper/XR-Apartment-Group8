#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.Tests
{
    public sealed class ReviewV2PanelPlayTests
    {
        Scene scene;
        ReviewIntegrationPanel panel;
        readonly List<GameObject> hidden = new List<GameObject>();
        bool hadPreference;
        int preference;
        GameObject extraSession;

        string feedbackDirectory, previousFeedbackDirectory;
        [UnitySetUp] public IEnumerator Load()
        {
            previousFeedbackDirectory = System.Environment.GetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY");
            feedbackDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReviewV2-regression-" + System.Guid.NewGuid().ToString("N"));
            System.Environment.SetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY", feedbackDirectory);

            hadPreference = PlayerPrefs.HasKey(KitchenLocomotionSettings.PreferenceKey);
            preference = PlayerPrefs.GetInt(KitchenLocomotionSettings.PreferenceKey);
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.activeSelf && (root.GetComponentInChildren<XROrigin>(true) != null ||
                    root.GetComponentInChildren<EventSystem>(true) != null || root.GetComponentInChildren<XRInteractionManager>(true) != null))
                { hidden.Add(root); root.SetActive(false); }
            scene = EditorSceneManager.LoadSceneInPlayMode(ReviewV2Scene, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!scene.isLoaded) yield return null;
            yield return null;
            panel = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewIntegrationPanel>(true)).Single();
        }
        const string ReviewV2Scene = "Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity";

        [UnityTearDown] public IEnumerator Cleanup()
        {
            System.Environment.SetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY", previousFeedbackDirectory);
            if (System.IO.Directory.Exists(feedbackDirectory)) System.IO.Directory.Delete(feedbackDirectory, true);

            if (extraSession != null) { Object.Destroy(extraSession); yield return null; yield return null; }
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var go in hidden) if (go != null) go.SetActive(true);
            hidden.Clear(); Time.timeScale = 1;
            if (hadPreference) PlayerPrefs.SetInt(KitchenLocomotionSettings.PreferenceKey, preference);
            else PlayerPrefs.DeleteKey(KitchenLocomotionSettings.PreferenceKey);
        }

        [UnityTest] public IEnumerator InitialSetupFitsViewAndOnlyShowsRelevantActions()
        {
            Assert.AreEqual(SessionPhase.Setup, panel.host.Snapshot.Phase);
            Assert.IsTrue(panel.setup.gameObject.activeSelf);
            Assert.IsFalse(panel.review.gameObject.activeSelf);
            Assert.IsFalse(panel.finish.gameObject.activeSelf);
            // Match a desktop Game view, including looking down before opening the menu.
            var camera = panel.host.locomotion.Origin.Camera;
            panel.SetOpen(false);
            var hd = camera.GetComponent<HDAdditionalCameraData>();
            hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing;
            camera.aspect = 16f / 9; camera.fieldOfView = 60;
            camera.transform.rotation = Quaternion.Euler(18, 32, 0);
            panel.SetOpen(true);
            Assert.AreEqual(HDAdditionalCameraData.AntialiasingMode.None, hd.antialiasing);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var occupied = new List<Rect>();
            var parent = (RectTransform)panel.panel.transform;
            foreach (var button in panel.panel.GetComponentsInChildren<Button>())
            {
                var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var v = camera.WorldToViewportPoint(corner);
                    Assert.Greater(v.z, 0); Assert.That(v.x, Is.InRange(.02f, .98f), button.name);
                    Assert.That(v.y, Is.InRange(.02f, .98f), button.name);
                    Assert.IsTrue(parent.rect.Contains(parent.InverseTransformPoint(corner)), button.name);
                }
                var min = parent.InverseTransformPoint(corners[0]); var max = parent.InverseTransformPoint(corners[2]);
                var rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                foreach (var other in occupied) Assert.IsFalse(rect.Overlaps(other), button.name);
                occupied.Add(rect);
            }
            foreach (var text in panel.panel.GetComponentsInChildren<TMP_Text>())
            { Assert.GreaterOrEqual(text.fontSize, 28, text.text); Assert.IsFalse(text.isTextOverflowing, text.text); }
            panel.SetOpen(false);
            Assert.AreEqual(HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing, hd.antialiasing);
            panel.SetOpen(true); panel.enabled = false;
            Assert.AreEqual(HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing, hd.antialiasing);
            panel.enabled = true;
            Assert.AreEqual(HDAdditionalCameraData.AntialiasingMode.None, hd.antialiasing);
        }

        [UnityTest] public IEnumerator ButtonsDriveRealPhasesPauseConfirmationAndUnsavedFinish()
        {
            panel.setup.onClick.Invoke();
            Assert.AreEqual(SessionPhase.Onboarding, panel.host.Snapshot.Phase);
            Assert.IsTrue(panel.onboarding.gameObject.activeSelf); Assert.IsFalse(panel.review.gameObject.activeSelf);
            Assert.IsTrue(panel.panel.activeSelf);
            panel.onboarding.onClick.Invoke();
            Assert.AreEqual(SessionPhase.Experience, panel.host.Snapshot.Phase); Assert.IsFalse(panel.panel.activeSelf);
            panel.SetOpen(true); panel.pause.onClick.Invoke();
            Assert.IsTrue(panel.host.Snapshot.IsPaused); Assert.IsTrue(panel.smooth.gameObject.activeSelf);
            panel.smooth.onClick.Invoke();
            Assert.AreEqual(KitchenMovementMode.Smooth, panel.host.locomotion.Mode);
            Assert.IsTrue(panel.host.locomotion.MenuOpen);
            panel.resume.onClick.Invoke(); panel.SetOpen(true);
            var visit = panel.host.Snapshot.SchemeVisitId;
            panel.reset.onClick.Invoke(); Assert.IsTrue(panel.confirmReset.gameObject.activeSelf);
            Assert.IsFalse(panel.close.gameObject.activeSelf);
            panel.cancelReset.onClick.Invoke();
            Assert.AreEqual(visit, panel.host.Snapshot.SchemeVisitId);
            Assert.IsFalse(panel.confirmReset.gameObject.activeSelf); Assert.IsTrue(panel.review.gameObject.activeSelf);
            panel.review.onClick.Invoke(); Assert.AreEqual(SessionPhase.Review, panel.host.Snapshot.Phase);
            Assert.IsTrue(panel.back.gameObject.activeSelf); Assert.IsFalse(panel.reset.gameObject.activeSelf);
            panel.back.onClick.Invoke(); Assert.AreEqual(visit, panel.host.Snapshot.SchemeVisitId);
            panel.SetOpen(true); panel.review.onClick.Invoke(); panel.finish.onClick.Invoke();
            Assert.AreEqual(SessionPhase.Finished, panel.host.Snapshot.Phase);
            Assert.AreEqual(SaveStatus.Verified, panel.host.Snapshot.SaveStatus);
            StringAssert.Contains("保存已验证", panel.status.text);
            Assert.AreEqual(1, panel.panel.GetComponentsInChildren<Button>().Length);
            yield return null;
        }

        void StartExperience(ReviewIntegrationPanel view)
        {
            view.setup.onClick.Invoke(); view.onboarding.onClick.Invoke(); view.SetOpen(true);
        }

        [UnityTest] public IEnumerator ActivityBriefShowsRealGoalsAndOpeningDoesNotChangeWorldOrVisit()
        {
            panel.setup.onClick.Invoke();
            var brief = panel.activityBrief;
            Assert.IsTrue(brief.gameObject.activeSelf, "Onboarding includes the experience brief");
            Assert.IsTrue(panel.exit.gameObject.activeSelf);
            Assert.IsTrue(panel.close.gameObject.activeSelf);
            StringAssert.Contains("活动顺序自由", brief.introduction.text);
            Assert.AreEqual("尚未开始", brief.goalStates[0].text);
            Assert.AreEqual("尚未开始", brief.goalStates[1].text);
            for (int i = 2; i < 5; i++) Assert.AreEqual("未接入", brief.goalStates[i].text);
            panel.recenter.onClick.Invoke();
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.IsFalse(panel.status.isTextOverflowing, "Calibration result remains readable in the brief");
            var camera = panel.host.locomotion.Origin.Camera;
            foreach (var aspect in new[] { 16f / 9, 4f / 3 })
            {
                camera.aspect = aspect; camera.fieldOfView = 60;
                AssertButtonsInView(panel.panel, camera);
            }
            panel.onboarding.onClick.Invoke();
            Assert.IsFalse(brief.gameObject.activeSelf);
            var host = panel.host; var visit = host.Snapshot.SchemeVisitId;
            var tomato = host.Resolve("OBJECT_030").GetComponent<PreparedKitchenProp>();
            var position = tomato.transform.position;
            panel.quickBrief.onClick.Invoke();
            Assert.IsTrue(brief.gameObject.activeInHierarchy);
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
            Assert.AreEqual(position, tomato.transform.position);
            panel.briefBack.onClick.Invoke();
            Assert.IsFalse(panel.panel.activeSelf);
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
            Assert.IsTrue(tomato.TryCut(100f), "Real food component starts the activity");
            yield return null; yield return null;
            Assert.AreEqual(GoalState.InProgress, host.Snapshot.Goals.Single(g => g.Id == "G02").State);
            panel.SetOpen(true); panel.brief.onClick.Invoke();
            Assert.AreEqual("进行中", brief.goalStates[1].text, "Reopening reads the current snapshot");
            panel.review.onClick.Invoke();
            Assert.AreEqual(SessionPhase.Review, host.Snapshot.Phase);
            Assert.IsFalse(brief.gameObject.activeSelf);
            panel.back.onClick.Invoke(); panel.quickBrief.onClick.Invoke();
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
            Assert.AreEqual("进行中", brief.goalStates[1].text);
            panel.enabled = false; panel.enabled = true;
            Assert.IsFalse(brief.gameObject.activeSelf, "Re-enable does not resurrect a stale page");
        }

        [UnityTest] public IEnumerator ActivityBriefUpdatesWhileVisibleAndSwitchReadsNewVisit()
        {
            StartExperience(panel); panel.brief.onClick.Invoke();
            var host = panel.host;
            Assert.IsTrue(host.Resolve("OBJECT_030").GetComponent<PreparedKitchenProp>().TryCut(100f));
            yield return null; yield return null;
            Assert.AreEqual("进行中", panel.activityBrief.goalStates[1].text, "Changed updates the visible card");
            panel.briefBack.onClick.Invoke(); panel.SetOpen(true);
            panel.changeScheme.onClick.Invoke(); panel.confirmScheme.onClick.Invoke();
            for (int i = 0; i < 60 && host.Snapshot.IsBusy; i++) yield return null;
            Assert.AreEqual(SchemeIds.ExternalRefrigerator, host.Snapshot.SchemeId);
            panel.brief.onClick.Invoke();
            StringAssert.Contains("方案 B", panel.status.text);
            Assert.AreEqual("尚未开始", panel.activityBrief.goalStates[1].text);
            // Presenter-only fixture: unavailable takes priority over a stale completed state,
            // and an omitted Goal is unavailable rather than a user failure.
            var s = host.Snapshot;
            panel.activityBrief.Present(new SessionSnapshot(s.SessionId, s.Phase, s.SchemeId, s.SchemeVisitId,
                s.ModelVersion, new[] { new GoalSnapshot("G01", GoalState.Completed, true),
                    new GoalSnapshot("G02", GoalState.Completed, false) }, false, false, false, s.SaveStatus));
            Assert.AreEqual("已完成", panel.activityBrief.goalStates[0].text);
            Assert.AreEqual("未接入", panel.activityBrief.goalStates[1].text);
            Assert.AreEqual("未接入", panel.activityBrief.goalStates[2].text);
            host.Execute(CommandType.Pause);
            Assert.IsFalse(panel.activityBrief.gameObject.activeSelf);
            panel.OpenBrief(); Assert.IsFalse(panel.activityBrief.gameObject.activeSelf);
        }

        [UnityTest] public IEnumerator SchemeMenuCancelsWithoutChangesAndActuallyLoadsBThenA()
        {
            Assert.IsFalse(panel.changeScheme.gameObject.activeSelf);
            StartExperience(panel);
            var host = panel.host; var port = host.GetComponent<ReviewSchemePort>();
            Assert.AreEqual(SchemeIds.IntegratedKitchen, host.Snapshot.SchemeId);
            var world = port.Current; var visit = host.Snapshot.SchemeVisitId; var session = host.Snapshot.SessionId;
            panel.changeScheme.onClick.Invoke();
            Assert.IsTrue(panel.confirmScheme.gameObject.activeSelf); Assert.IsFalse(panel.confirmReset.gameObject.activeSelf);
            StringAssert.Contains("活动进度重置", panel.status.text); StringAssert.Contains("存储已接入", panel.status.text);
            Canvas.ForceUpdateCanvases(); Assert.IsFalse(panel.status.isTextOverflowing);
            panel.cancelScheme.onClick.Invoke();
            Assert.AreSame(world, port.Current); Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
            Assert.IsFalse(panel.confirmScheme.gameObject.activeSelf);
            panel.changeScheme.onClick.Invoke(); panel.confirmScheme.onClick.Invoke();
            Assert.AreEqual(SchemeIds.ExternalRefrigerator, host.Snapshot.SchemeId);
            Assert.AreEqual(port.ModelVersionFor(SchemeIds.ExternalRefrigerator), host.Snapshot.ModelVersion);
            Assert.AreNotSame(world, port.Current); Assert.AreNotEqual(visit, host.Snapshot.SchemeVisitId);
            Assert.AreEqual(session, host.Snapshot.SessionId); Assert.AreEqual(SaveStatus.Verified, host.Snapshot.SaveStatus);
            StringAssert.Contains("已切换至方案 B", panel.status.text);
            StringAssert.Contains("方案 A", panel.changeScheme.GetComponentInChildren<TMP_Text>().text);
            Assert.IsTrue(host.Snapshot.Goals.All(g => g.State == GoalState.NotStarted));
            var bVisit = host.Snapshot.SchemeVisitId;
            panel.changeScheme.onClick.Invoke(); panel.confirmScheme.onClick.Invoke();
            Assert.AreEqual(SchemeIds.IntegratedKitchen, host.Snapshot.SchemeId);
            Assert.AreEqual(port.ModelVersionFor(SchemeIds.IntegratedKitchen), host.Snapshot.ModelVersion);
            Assert.AreNotEqual(bVisit, host.Snapshot.SchemeVisitId); Assert.IsFalse(host.station.ActiveFridge.Accessible);
            StringAssert.Contains("已切换至方案 A", panel.status.text);
            panel.pause.onClick.Invoke(); Assert.IsFalse(panel.changeScheme.gameObject.activeSelf);
            panel.resume.onClick.Invoke(); panel.SetOpen(true); panel.review.onClick.Invoke();
            Assert.IsFalse(panel.changeScheme.gameObject.activeSelf);
            yield return null;
        }

        [UnityTest] public IEnumerator SchemeMenuReportsObstructionWithoutChangingWorldAndAllowsRetry()
        {
            StartExperience(panel);
            var host = panel.host; var port = host.GetComponent<ReviewSchemePort>();
            var world = port.Current; var visit = host.Snapshot.SchemeVisitId;
            var camera = host.locomotion.Origin.Camera.transform; var before = camera.position;
            var target = port.layouts.Single(w => w.schemeId == SchemeIds.ExternalRefrigerator);
            camera.position = target.station.ActiveFridge.transform.TransformPoint(new Vector3(.34f, 1.2f, 0));
            try
            {
                panel.changeScheme.onClick.Invoke(); panel.confirmScheme.onClick.Invoke();
                Assert.AreSame(world, port.Current); Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
                StringAssert.Contains("切换失败", panel.status.text);
                StringAssert.DoesNotContain("已切换至", panel.status.text);
                Assert.IsTrue(panel.changeScheme.interactable); Assert.IsFalse(host.Snapshot.IsBusy);
            }
            finally { camera.position = before; }
            panel.changeScheme.onClick.Invoke(); panel.confirmScheme.onClick.Invoke();
            Assert.AreEqual(SchemeIds.ExternalRefrigerator, host.Snapshot.SchemeId);
            StringAssert.Contains("已切换至方案 B", panel.status.text);
            yield return null;
        }

        [UnityTest] public IEnumerator SchemeMenuWaitsForMatchingSaveResultAndKeepsFailureRetryable()
        {
            // Synthetic save callbacks only: verifies UI waiting/failure, never durable storage.
            var source = panel.host; panel.gameObject.SetActive(false);
            source.GetComponent<ReviewSchemePort>().Current.gameObject.SetActive(false);
            extraSession = new GameObject("Panel pending-save fixture"); extraSession.SetActive(false);
            extraSession.AddComponent<XRInteractionManager>();
            var host = extraSession.AddComponent<ReviewSessionHost>(); host.locomotion = source.locomotion;
            var save = extraSession.AddComponent<ReviewSchemePendingSaveProbe>(); host.sessionSavePort = save;
            var port = extraSession.AddComponent<ReviewSchemePort>(); port.host = host;
            port.layouts = source.GetComponent<ReviewSchemePort>().layouts;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2Integration.prefab");
            var view = Object.Instantiate(prefab, extraSession.transform).GetComponent<ReviewIntegrationPanel>();
            view.host = host; view.resetButton = null;
            view.gameObject.SetActive(true); extraSession.SetActive(true);
            yield return null;
            StartExperience(view);
            var world = port.Current; var visit = host.Snapshot.SchemeVisitId;
            view.changeScheme.onClick.Invoke(); view.confirmScheme.onClick.Invoke();
            Assert.IsTrue(host.Snapshot.IsBusy); Assert.IsFalse(view.changeScheme.interactable);
            view.OpenMark(); Assert.IsFalse(view.markDraft.IsOpen);
            Assert.IsFalse(view.confirmScheme.gameObject.activeSelf);
            StringAssert.Contains("正在切换", view.status.text); StringAssert.DoesNotContain("已切换至", view.status.text);
            save.Complete("wrong-request", false, "ignored callback"); Assert.IsTrue(host.Snapshot.IsBusy);
            save.Complete(save.Id, false, "synthetic write failure");
            Assert.AreSame(world, port.Current); Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
            StringAssert.Contains("synthetic write failure", view.status.text); Assert.IsTrue(view.changeScheme.interactable);
            view.changeScheme.onClick.Invoke(); view.confirmScheme.onClick.Invoke();
            save.Complete(save.Id, false, "synthetic retry failure");
            Assert.AreSame(world, port.Current); Assert.IsFalse(host.Snapshot.IsBusy);
            StringAssert.Contains("synthetic retry failure", view.status.text);
        }

        [UnityTest] public IEnumerator ConnectedFeedbackPagesSupportDraftDiscardAndZeroMarkerFinish()
        {
            Assert.IsNotNull(panel.markDraft.t3Source); Assert.IsNotNull(panel.reviewBoard.t3Source);
            Assert.IsNotNull(panel.host.sessionSavePort);
            panel.OpenMark(); Assert.IsFalse(panel.markDraft.IsOpen);
            panel.setup.onClick.Invoke(); panel.onboarding.onClick.Invoke();
            Assert.IsTrue(panel.quickBar.activeSelf); Assert.IsFalse(panel.panel.activeSelf);
            Assert.IsFalse(panel.host.locomotion.MenuOpen);
            var visit = panel.host.Snapshot.SchemeVisitId;
            panel.quickMark.onClick.Invoke();
            Assert.AreEqual(SessionPhase.Experience, panel.host.Snapshot.Phase);
            Assert.IsTrue(panel.markDraft.IsOpen); Assert.IsTrue(panel.host.locomotion.MenuOpen);
            Assert.IsFalse(panel.quickBar.activeSelf);
            Assert.IsFalse(panel.markDraft.submit.interactable); Assert.IsTrue(panel.markDraft.capture.interactable);
            panel.markDraft.note.text = "柜门开启后这里不方便通过。这是未保存的试填草稿。";
            panel.markDraft.cancel.onClick.Invoke();
            Assert.IsTrue(panel.markDraft.IsConfirmingDiscard); Assert.IsFalse(panel.markDraft.editor.interactable);
            panel.markDraft.keepEditing.onClick.Invoke();
            Assert.IsFalse(panel.markDraft.IsConfirmingDiscard); Assert.IsTrue(panel.markDraft.HasDraft);
            panel.SetOpen(false); Assert.IsTrue(panel.markDraft.IsConfirmingDiscard);
            panel.markDraft.discard.onClick.Invoke();
            Assert.IsFalse(panel.markDraft.IsOpen); Assert.IsFalse(panel.markDraft.HasDraft);
            Assert.IsTrue(panel.quickBar.activeSelf); Assert.AreEqual(visit, panel.host.Snapshot.SchemeVisitId);
            panel.quickReview.onClick.Invoke();
            Assert.AreEqual(SessionPhase.Review, panel.host.Snapshot.Phase);
            Assert.AreEqual(ReviewBoardDisplayState.Empty, panel.reviewBoard.State);
            panel.OpenMark(); Assert.IsFalse(panel.markDraft.IsOpen);
            StringAssert.Contains("暂无标记", panel.reviewBoard.listText.text);
            Assert.IsTrue(panel.reviewBoard.currentScheme.interactable); Assert.IsTrue(panel.reviewBoard.allSchemes.interactable);
            panel.back.onClick.Invoke(); Assert.AreEqual(visit, panel.host.Snapshot.SchemeVisitId);
            panel.quickReview.onClick.Invoke(); panel.finish.onClick.Invoke();
            Assert.AreEqual(SessionPhase.Finished, panel.host.Snapshot.Phase);
            Assert.AreEqual(SaveStatus.Verified, panel.host.Snapshot.SaveStatus);
            StringAssert.Contains("保存已验证", panel.status.text);
            panel.OpenMark(); Assert.IsFalse(panel.markDraft.IsOpen);
            yield return null;
        }

        [UnityTest] public IEnumerator DraftIsInvalidatedByPauseResetAndSchemeChangesAndDoesNotSurviveDisable()
        {
            StartExperience(panel); panel.OpenMark(); panel.markDraft.note.text = "pause draft";
            panel.host.Execute(CommandType.Pause);
            Assert.IsFalse(panel.markDraft.IsOpen); Assert.IsFalse(panel.markDraft.HasDraft);
            Assert.IsFalse(panel.quickBar.activeSelf); StringAssert.Contains("草稿已清除", panel.status.text);
            panel.OpenMark(); Assert.IsFalse(panel.markDraft.IsOpen);
            panel.host.Execute(CommandType.Resume);
            panel.OpenMark(); panel.markDraft.note.text = "reset draft";
            var visit = panel.host.Snapshot.SchemeVisitId;
            Assert.IsTrue(panel.host.Execute(CommandType.ResetWorld).Accepted);
            Assert.IsFalse(panel.markDraft.IsOpen); Assert.IsFalse(panel.markDraft.HasDraft);
            for (int i = 0; i < 180 && panel.host.Snapshot.IsBusy; i++) yield return null;
            Assert.IsFalse(panel.host.Snapshot.IsBusy); Assert.AreNotEqual(visit, panel.host.Snapshot.SchemeVisitId);
            panel.OpenMark(); panel.markDraft.note.text = "scheme draft";
            panel.host.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator);
            Assert.AreEqual(SchemeIds.ExternalRefrigerator, panel.host.Snapshot.SchemeId);
            Assert.IsFalse(panel.markDraft.IsOpen); Assert.IsFalse(panel.markDraft.HasDraft);
            panel.OpenMark(); panel.markDraft.note.text = "disable draft";
            panel.enabled = false;
            Assert.IsFalse(panel.panel.activeSelf); Assert.IsFalse(panel.quickBar.activeSelf);
            Assert.IsFalse(panel.markDraft.HasDraft);
            panel.enabled = true;
            Assert.IsFalse(panel.markDraft.IsOpen);
            Assert.AreEqual(SessionPhase.Experience, panel.host.Snapshot.Phase);
        }

        [UnityTest] public IEnumerator ReviewBoardDisplayStatesAndLongTextHaveDistinctScrollablePresentation()
        {
            StartExperience(panel); panel.review.onClick.Invoke();
            var board = panel.reviewBoard;
            Canvas.ForceUpdateCanvases();
            Assert.IsFalse(board.currentScheme.GetComponentInChildren<TMP_Text>().isTextOverflowing);
            Assert.IsFalse(board.allSchemes.GetComponentInChildren<TMP_Text>().isTextOverflowing);
            board.Present(ReviewBoardDisplayState.Loading); StringAssert.Contains("正在读取", board.listText.text);
            board.Present(ReviewBoardDisplayState.Empty); StringAssert.Contains("暂无标记", board.listText.text);
            board.Present(ReviewBoardDisplayState.Failed); StringAssert.Contains("读取失败", board.listText.text);
            // Presentation-only fixture strings, not Marker records or a storage stub.
            string longText = string.Join("\n", Enumerable.Repeat("这是一段用于检查长备注滚动显示的界面文字。", 35));
            board.Present(ReviewBoardDisplayState.Ready, longText, longText);
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.Greater(board.listScroll.content.rect.height, board.listScroll.viewport.rect.height);
            Assert.Greater(board.detailScroll.content.rect.height, board.detailScroll.viewport.rect.height);
            board.detailScroll.verticalNormalizedPosition = 0; yield return null;
            Assert.That(board.detailScroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.02f));
            board.ShowUnavailable(); Assert.AreEqual(ReviewBoardDisplayState.NotConnected, board.State);
            StringAssert.DoesNotContain("暂无标记", board.listText.text);
        }

        [UnityTest] public IEnumerator FeedbackPagesAndShortcutsFitNormalAndNarrowViews()
        {
            panel.setup.onClick.Invoke(); panel.onboarding.onClick.Invoke();
            var camera = panel.host.locomotion.Origin.Camera;
            foreach (var aspect in new[] { 16f / 9, 4f / 3 })
            {
                camera.aspect = aspect; camera.fieldOfView = 60;
                camera.transform.rotation = Quaternion.Euler(15, 28, 0);
                panel.SetOpen(false); yield return null;
                AssertButtonsInView(panel.quickBar, camera);
                panel.quickBrief.onClick.Invoke(); yield return null; Canvas.ForceUpdateCanvases();
                AssertButtonsInView(panel.panel, camera);
                foreach (var text in panel.panel.GetComponentsInChildren<TMP_Text>())
                {
                    Assert.IsFalse(text.isTextOverflowing, text.text);
                    var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = camera.WorldToViewportPoint(corner);
                        Assert.That(point.x, Is.InRange(.02f, .98f), text.name);
                        Assert.That(point.y, Is.InRange(.02f, .98f), text.name);
                    }
                }
                panel.briefBack.onClick.Invoke();
                panel.OpenMark(); yield return null; Canvas.ForceUpdateCanvases();
                Assert.IsFalse(panel.status.isTextOverflowing, "Mark heading");
                AssertButtonsInView(panel.panel, camera);
                panel.markDraft.cancel.onClick.Invoke(); panel.quickReview.onClick.Invoke();
                yield return null; Canvas.ForceUpdateCanvases();
                Assert.IsFalse(panel.status.isTextOverflowing, "Review heading");
                AssertButtonsInView(panel.panel, camera);
                panel.back.onClick.Invoke();
            }
        }
        static void AssertButtonsInView(GameObject root, Camera camera)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
            {
                var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = camera.WorldToViewportPoint(corner);
                    Assert.Greater(point.z, 0, button.name);
                    Assert.That(point.x, Is.InRange(.02f, .98f), button.name);
                    Assert.That(point.y, Is.InRange(.02f, .98f), button.name);
                }
            }
        }
    }
}
#endif
