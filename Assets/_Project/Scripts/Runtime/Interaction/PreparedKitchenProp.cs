using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    public enum PreparedPropKind { Fracture, Slice, Eggshell }

    /// Switches an intact prop to pieces derived in Blender. Pieces are reused on reset.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PreparedKitchenProp : MonoBehaviour
    {
        [SerializeField] PreparedPropSettings settings;
        [SerializeField] PreparedPropKind kind;
        [SerializeField] Transform intactRoot;
        [SerializeField] Transform piecesRoot;
        [SerializeField] Rigidbody[] pieces = Array.Empty<Rigidbody>();
        [SerializeField] ParticleSystem splitParticles;
        [SerializeField] AudioSource splitAudio;

        Rigidbody body;
        XRGrabInteractable grab;
        Collider[] intactColliders;
        bool[] colliderEnabled;
        Vector3[] restPositions;
        Quaternion[] restRotations;
        XRGrabInteractable[] pieceGrabs;
        bool initialized;
        bool originalKinematic;
        bool originalGravity;
        float recycleAt;

        public bool IsSplit { get; private set; }
        public PreparedPropKind Kind => kind;
        public IReadOnlyList<Rigidbody> Pieces => pieces;
        public event Action<PreparedKitchenProp> Split;

        public void Configure(PreparedPropSettings profile, PreparedPropKind mode, Transform intact,
            Transform fragments, Rigidbody[] fragmentBodies)
        {
            if (initialized) throw new InvalidOperationException("Configure before using the prop.");
            settings = profile;
            kind = mode;
            intactRoot = intact;
            piecesRoot = fragments;
            pieces = fragmentBodies;
            if (Application.isPlaying) Initialize();
        }

        void Awake()
        {
            if (settings != null && intactRoot != null && piecesRoot != null && pieces.Length >= 2) Initialize();
        }

        void Initialize()
        {
            if (initialized) return;
            if (settings == null || intactRoot == null || piecesRoot == null || pieces.Length < 2)
                throw new InvalidOperationException("Prepared prop is missing its profile or derived pieces.");
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            originalKinematic = body.isKinematic;
            originalGravity = body.useGravity;
            intactColliders = intactRoot.GetComponentsInChildren<Collider>(true);
            colliderEnabled = new bool[intactColliders.Length];
            for (var i = 0; i < intactColliders.Length; i++) colliderEnabled[i] = intactColliders[i].enabled;
            restPositions = new Vector3[pieces.Length];
            restRotations = new Quaternion[pieces.Length];
            pieceGrabs = new XRGrabInteractable[pieces.Length];
            for (var i = 0; i < pieces.Length; i++)
            {
                restPositions[i] = pieces[i].transform.localPosition;
                restRotations[i] = pieces[i].transform.localRotation;
                pieceGrabs[i] = pieces[i].GetComponent<XRGrabInteractable>();
                pieces[i].isKinematic = true;
                pieces[i].useGravity = false;
            }
            piecesRoot.gameObject.SetActive(false);
            initialized = true;
        }

        public bool TryImpact(float speed)
        {
            if (kind == PreparedPropKind.Slice || !Finite(speed) || settings == null || speed < settings.impactSpeed)
                return false;
            return SplitNow();
        }

        public bool TryCut(float speed)
        {
            if (kind != PreparedPropKind.Slice || !Finite(speed) || settings == null || speed < settings.knifeSpeed)
                return false;
            return SplitNow();
        }

        // An egg receiver can call this after validating a held egg's crack gesture.
        public bool TryCrack()
        {
            return kind == PreparedPropKind.Eggshell && SplitNow();
        }

        bool SplitNow()
        {
            if (IsSplit) return false;
            Initialize();
            IsSplit = true;
            var linear = body.isKinematic ? Vector3.zero : body.velocity;
            var angular = body.isKinematic ? Vector3.zero : body.angularVelocity;
            var centre = body.worldCenterOfMass;
            // Disabling the interactable cancels XRI selection before hiding the
            // physical intact shape. The tracking transformer remains the sole
            // authority for held poses up to this point.
            if (grab != null) grab.enabled = false;
            // Detached food must stay in the same interaction context as its
            // intact parent, including additive scenes and explicit managers.
            if (grab != null && grab.interactionManager != null)
                foreach (var pieceGrab in pieceGrabs) if (pieceGrab != null) pieceGrab.interactionManager = grab.interactionManager;
            body.isKinematic = true;
            body.useGravity = false;
            foreach (var collider in intactColliders) collider.enabled = false;
            intactRoot.gameObject.SetActive(false);
            piecesRoot.SetParent(null, true);
            piecesRoot.gameObject.SetActive(true);
            foreach (var pieceGrab in pieceGrabs)
                if (pieceGrab != null) pieceGrab.SetTargetPose(new Pose(pieceGrab.transform.position, pieceGrab.transform.rotation));
            Physics.SyncTransforms();
            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                piece.isKinematic = false;
                piece.useGravity = true;
                var offset = piece.worldCenterOfMass - centre;
                piece.velocity = Vector3.ClampMagnitude(linear + Vector3.Cross(angular, offset) +
                    offset.normalized * settings.separationSpeed, settings.maxFragmentSpeed);
                piece.angularVelocity = angular;
            }
            recycleAt = kind == PreparedPropKind.Slice ? float.PositiveInfinity : Time.time + settings.fragmentLifetime;
            if (splitParticles != null) splitParticles.Play();
            if (splitAudio != null && splitAudio.clip != null) splitAudio.Play();
            Split?.Invoke(this);
            return true;
        }

        public void ResetProp()
        {
            if (!initialized) return;
            piecesRoot.gameObject.SetActive(false);
            piecesRoot.SetParent(transform, false);
            piecesRoot.localPosition = Vector3.zero;
            piecesRoot.localRotation = Quaternion.identity;
            piecesRoot.localScale = Vector3.one;
            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                if (!piece.isKinematic) { piece.velocity = Vector3.zero; piece.angularVelocity = Vector3.zero; }
                piece.isKinematic = true;
                piece.useGravity = false;
                piece.transform.localPosition = restPositions[i];
                piece.transform.localRotation = restRotations[i];
                if (pieceGrabs[i] != null) pieceGrabs[i].SetTargetPose(new Pose(piece.transform.position, piece.transform.rotation));
            }
            intactRoot.gameObject.SetActive(true);
            for (var i = 0; i < intactColliders.Length; i++) intactColliders[i].enabled = colliderEnabled[i];
            body.isKinematic = originalKinematic;
            body.useGravity = originalGravity;
            IsSplit = false;
            if (grab != null) { grab.SetTargetPose(new Pose(transform.position, transform.rotation)); grab.enabled = true; }
        }

        void OnCollisionEnter(Collision collision) => TryImpact(collision.relativeVelocity.magnitude);

        void Update()
        {
            if (IsSplit && piecesRoot != null && piecesRoot.gameObject.activeSelf && Time.time >= recycleAt)
                piecesRoot.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (initialized && piecesRoot != null && piecesRoot.parent != transform)
                piecesRoot.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (initialized && piecesRoot != null && piecesRoot.parent != transform)
                Destroy(piecesRoot.gameObject);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
