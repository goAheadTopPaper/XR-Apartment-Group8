using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRApartment.Runtime.ReviewV2.UI
{
    /// <summary>Owns panel visibility only. It deliberately contains no session or phase logic.</summary>
    public sealed class ReviewV2UiController : MonoBehaviour
    {
        [Serializable]
        public sealed class PanelRegistration
        {
            public string id;
            public GameObject panel;
        }

        [SerializeField] PanelRegistration[] panels = Array.Empty<PanelRegistration>();
        readonly Dictionary<string, GameObject> panelsById = new Dictionary<string, GameObject>();
        public event Action VisibilityChanged;
        bool lastVisible;
        public bool AnyPanelVisible
        {
            get { foreach (var p in panelsById.Values) if (p != null && p.activeInHierarchy) return true; return false; }
        }
        void NotifyVisibility()
        {
            bool visible = AnyPanelVisible;
            if (lastVisible == visible) return;
            lastVisible = visible;
            VisibilityChanged?.Invoke();
        }
        void LateUpdate() => NotifyVisibility();
        void ClearHiddenFocus()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            if (selected == null || selected.activeInHierarchy) return;
            foreach (var p in panelsById.Values)
                if (p != null && (selected == p || selected.transform.IsChildOf(p.transform)))
                { events.SetSelectedGameObject(null); break; }
        }

        void Awake()
        {
            panelsById.Clear();
            foreach (var registration in panels)
            {
                if (registration == null || string.IsNullOrWhiteSpace(registration.id) || registration.panel == null)
                    continue;
                if (!panelsById.ContainsKey(registration.id)) panelsById.Add(registration.id, registration.panel);
            }
        }

        public bool SetPanelVisible(string id, bool visible)
        {
            if (string.IsNullOrWhiteSpace(id) || !panelsById.TryGetValue(id, out var panel) || panel == null)
                return false;
            panel.SetActive(visible);
            ClearHiddenFocus();
            NotifyVisibility();
            return true;
        }

        public bool ShowPanel(string id)
        {
            if (!panelsById.ContainsKey(id)) return false;
            foreach (var entry in panelsById)
                if (entry.Value != null) entry.Value.SetActive(entry.Key == id);
            ClearHiddenFocus();
            NotifyVisibility();
            return true;
        }

        public void HideAll()
        {
            foreach (var panel in panelsById.Values)
                if (panel != null) panel.SetActive(false);
            ClearHiddenFocus();
            NotifyVisibility();
        }
    }
}
