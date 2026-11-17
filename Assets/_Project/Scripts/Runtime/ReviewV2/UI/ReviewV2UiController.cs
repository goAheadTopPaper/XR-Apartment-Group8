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
            return true;
        }

        public bool ShowPanel(string id)
        {
            if (!panelsById.ContainsKey(id)) return false;
            foreach (var entry in panelsById)
                if (entry.Value != null) entry.Value.SetActive(entry.Key == id);
            return true;
        }

        public void HideAll()
        {
            foreach (var panel in panelsById.Values)
                if (panel != null) panel.SetActive(false);
        }
    }
}
