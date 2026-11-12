using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace XRApartment.Runtime.UI
{
    public sealed class KitchenPauseMenu : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
    {
        [SerializeField] LocomotionSettingsView menu;
        [SerializeField] KitchenLocomotionSettings movement;
        [SerializeField] KitchenActivityGuide guide;
        [SerializeField] KitchenFeedbackNotes notes;
        [SerializeField] GameObject panel;
        [SerializeField] Button resume, kitchenGuide, layout, feedback, options, graphics;
        [SerializeField] KitchenGraphicsMenu graphicsMenu;
        [SerializeField] TMP_Text resumeLabel, stageTitle, stageInstruction;
        [SerializeField] Canvas[] recipeCards = Array.Empty<Canvas>();
        XRBaseInteractor[] interactors;
        NearFarInteractor[] rays;
        bool[] nearCasting, recipeCardsEnabled;
        float previousTimeScale;
        bool previousAudioPause;

        public bool IsPaused { get; private set; }
        internal bool AllowsLayout => guide.AllowsLayout;
        internal bool AllowsFreePlay => !guide.isActiveAndEnabled || guide.IsFreePlay;
        internal bool HasSelection => Array.Exists(interactors, interactor => interactor != null && interactor.hasSelection);
        bool IXRHoverFilter.canProcess => IsPaused;
        bool IXRSelectFilter.canProcess => IsPaused;
        bool IXRHoverFilter.Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => false;
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => false;

        void Awake()
        {
            interactors = movement.GetComponentsInChildren<XRBaseInteractor>(true);
            rays = movement.GetComponentsInChildren<NearFarInteractor>(true);
            nearCasting = new bool[rays.Length];
            recipeCardsEnabled = new bool[recipeCards.Length];
            resume.onClick.AddListener(Resume);
            kitchenGuide.onClick.AddListener(guide.Open);
            layout.onClick.AddListener(() => menu.OpenLayout(fromPauseMenu: true));
            feedback.onClick.AddListener(notes.OpenOrSpawn);
            options.onClick.AddListener(menu.Open);
            graphicsMenu.Bind(this);
            graphics.onClick.AddListener(() => graphicsMenu.Open(menu));
        }

        void OnEnable()
        {
            foreach (var interactor in interactors)
            {
                interactor.hoverFilters.Add(this);
                interactor.selectFilters.Add(this);
            }
        }

        void OnDisable()
        {
            SetPaused(false);
            foreach (var interactor in interactors)
            {
                if (interactor == null) continue;
                interactor.hoverFilters.Remove(this);
                interactor.selectFilters.Remove(this);
            }
        }

        internal void Toggle()
        {
            if (panel.activeSelf) Resume();
            else Open(recenter: true);
        }

        internal void Open(bool recenter = false)
        {
            var guiding = guide.isActiveAndEnabled;
            kitchenGuide.interactable = guiding;
            layout.interactable = guide.AllowsLayout;
            feedback.interactable = guide.AllowsFeedback;
            KitchenLocale.Set(resumeLabel, guide.RequiresChoice ? "Return to guide" : "Resume");
            KitchenLocale.Set(stageTitle, guiding ? guide.StageTitle : "Free play");
            KitchenLocale.Set(stageInstruction, guiding ? guide.StageInstruction : "Explore the kitchen at your own pace.");
            menu.OpenAuxiliary(panel, recenter);
        }

        void Resume()
        {
            if (guide.RequiresChoice) guide.Open();
            else menu.Close();
        }

        internal void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            for (var i = 0; i < recipeCards.Length; i++)
            {
                var card = recipeCards[i];
                if (card == null) continue;
                if (paused) recipeCardsEnabled[i] = card.enabled;
                card.enabled = !paused && recipeCardsEnabled[i];
            }
            if (paused)
            {
                previousTimeScale = Time.timeScale;
                previousAudioPause = AudioListener.pause;
                Time.timeScale = 0f;
                AudioListener.pause = true;
                for (var i = 0; i < rays.Length; i++)
                {
                    if (rays[i] == null) continue;
                    nearCasting[i] = rays[i].enableNearCasting;
                    rays[i].enableNearCasting = false;
                }
                ReleaseSelections();
            }
            else
            {
                Time.timeScale = previousTimeScale;
                AudioListener.pause = previousAudioPause;
                for (var i = 0; i < rays.Length; i++)
                    if (rays[i] != null) rays[i].enableNearCasting = nearCasting[i];
            }
        }

        void ReleaseSelections()
        {
            // Cancel the physical selections so either hand can immediately use its UI ray.
            var held = new Dictionary<XRGrabInteractable, bool>();
            foreach (var interactor in interactors)
            {
                if (interactor == null || !interactor.hasSelection || interactor.interactionManager == null) continue;
                foreach (var item in interactor.interactablesSelected)
                    if (item is XRGrabInteractable grab && held.TryAdd(grab, grab.throwOnDetach))
                        grab.throwOnDetach = false;
                interactor.interactionManager.CancelInteractorSelection((IXRSelectInteractor)interactor);
            }
            foreach (var item in held)
            {
                var grab = item.Key;
                if (grab == null) continue;
                if (!grab.isSelected)
                {
                    grab.ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase.Late);
                    var body = grab.GetComponent<Rigidbody>();
                    if (body != null && !body.isKinematic)
                    {
                        body.velocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                }
                grab.throwOnDetach = item.Value;
            }
        }
    }
}
