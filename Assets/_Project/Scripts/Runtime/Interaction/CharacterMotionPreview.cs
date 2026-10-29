using System;
using UnityEngine;

namespace XRApartment.Runtime
{
    /// Body-rig acceptance sample. This is not an NPC controller or a facial rig.
    public sealed class CharacterMotionPreview : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] string[] states = { "Stand", "Walk", "RaiseArm", "BendKnee" };
        [SerializeField] bool cycle = true;
        int current;
        float elapsed;
        public void Configure(Animator target, string[] clips) { animator = target; states = clips; }
        public void SetMotion(int motion)
        {
            if (motion < 0 || motion >= states.Length) throw new ArgumentOutOfRangeException(nameof(motion));
            current = motion; elapsed = 0;
            if (animator != null) animator.Play(states[current], 0, 0);
        }
        void Update()
        {
            if (!cycle || animator == null || !animator.isInitialized || states.Length == 0) return;
            elapsed += Time.deltaTime;
            if (elapsed >= 4) SetMotion((current + 1) % states.Length);
        }
    }
}
