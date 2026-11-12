using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XRApartment.Runtime
{
    public enum KitchenCue { Grip, Impact, SoftImpact, Unlatch, Close, Cut, Board, Stove, Menu, Step }

    /// Short spatial cues share a bounded voice pool in each kitchen.
    public sealed class KitchenAudio : MonoBehaviour
    {
        [SerializeField] AudioClip[] clips;
        readonly AudioSource[] voices = new AudioSource[6];
        static readonly List<KitchenAudio> Active = new List<KitchenAudio>();
        KitchenLocomotionSettings locomotion;
        Vector3 previousPosition;
        float distance;
        int next;

        public void Configure(AudioClip[] palette) => clips = palette;

        void Awake()
        {
            locomotion = GetComponent<KitchenLocomotionSettings>();
            previousPosition = transform.position;
            for (var i = 0; i < voices.Length; i++)
            {
                var voice = new GameObject("Kitchen audio voice").AddComponent<AudioSource>();
                voice.transform.SetParent(transform, false);
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.minDistance = .42f;
                voice.maxDistance = 7f;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.dopplerLevel = 0f;
                voices[i] = voice;
            }
        }

        void OnEnable() => Active.Add(this);
        void OnDisable()
        {
            Active.Remove(this);
            foreach (var voice in voices) if (voice != null) voice.Stop();
        }

        public static void Play(Scene scene, KitchenCue cue, Vector3 position, float volume = 1f)
        {
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var player = Active[i];
                if (player != null && player.gameObject.scene == scene)
                {
                    player.PlayCue(cue, position, volume);
                    return;
                }
            }
        }

        void PlayCue(KitchenCue cue, Vector3 position, float volume)
        {
            var index = (int)cue;
            if (clips == null || index >= clips.Length || clips[index] == null) return;
            var voice = voices[next];
            next = (next + 1) % voices.Length;
            voice.Stop();
            voice.transform.position = position;
            voice.pitch = .96f + .02f * (next % 5);
            voice.spatialBlend = cue == KitchenCue.Menu ? 0f : 1f;
            voice.ignoreListenerPause = cue == KitchenCue.Menu;
            voice.PlayOneShot(clips[index], Mathf.Clamp01(volume));
        }

        void LateUpdate()
        {
            var movement = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up).magnitude;
            previousPosition = transform.position;
            if (locomotion == null || locomotion.Character == null || !locomotion.Character.isGrounded ||
                locomotion.MenuOpen || movement > .35f)
            {
                distance = 0f;
                return;
            }
            distance += movement;
            if (distance < .58f) return;
            distance -= .58f;
            PlayCue(KitchenCue.Step, locomotion.Character.transform.position, .32f);
        }
    }
}
