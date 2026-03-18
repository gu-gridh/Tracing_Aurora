using System;
using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class MapControl : MonoBehaviour
{
    public Map map;

    [Header("GPS Trigger Distances (meters)")]
    public float enterDistanceMeters = 20f;
    public float exitDistanceMeters = 35f;
    public float switchCooldownSeconds = 2f;

    [Header("View Switching")]
    public GameObject xrOrigin;
    public Camera mapCamera;

    [Header("Tap-to-Spawn (XR)")]
    [Tooltip("Drag the Screen Space Ray Interactor (has XRRayInteractor).")]
    public XRRayInteractor arInteractor;

    [Tooltip("Drag the AR Camera transform here.")]
    public Transform xrCamera;

    public GameObject wolfPrefab;

    [Tooltip("Spawn only on HorizontalUp planes (floor/tables).")]
    public bool requireHorizontalUpSurface = true;

    [Header("Audio")]
    public AudioSource audioSource;

    [Serializable]
    public class MarkerAudio
    {
        [Tooltip("Must match the marker's label exactly (case-sensitive).")]
        public string label;
        public AudioClip clip;
    }

    [Tooltip("List of marker label -> audio clip mappings.")]
    public List<MarkerAudio> markerAudio = new List<MarkerAudio>();

    [Header("UI + Wolf Animation")]
    [Tooltip("GameObject to show while the player is in XR and has not placed the wolf yet.")]
    public GameObject spawnInstructionUI;

    [Tooltip("GameObject to show/hide (usually your button or a panel that contains the button).")]
    public GameObject wolfActionButton;

    [Tooltip("Animator trigger name to fire on the spawned wolf.")]
    public string wolfAnimatorTriggerName = "Action";

    private UserLocation userLocation;
    private bool isInside;
    private float nextAllowedSwitchTime;

    private GameObject wolfInstance;
    private Animator wolfAnimator;

    private string pendingMarkerLabel;
    private string lastPlayedLabel;

    private void Start()
    {
        userLocation = UserLocation.instance;

        if (!userLocation || !map || !xrOrigin || !mapCamera || !arInteractor || !xrCamera || !wolfPrefab)
        {
            Debug.LogError("MapControl: Missing required references.");
            enabled = false;
            return;
        }

        userLocation.OnLocationChanged += CheckArrival;

        //start in map view
        xrOrigin.SetActive(false);
        mapCamera.enabled = true;

        RefreshUIState();

        //logs
        var markers2D = map.marker2DManager.items;
        if (markers2D != null)
        {
            Debug.Log($"MapControl: Found {markers2D.Count} marker(s)");
            foreach (var m in markers2D)
            {
                double lon = m.location.x;
                double lat = m.location.y;
                string label = string.IsNullOrEmpty(m.label) ? "(no label)" : m.label;
                //Debug.Log($"Marker \"{label}\" → lat:{lat:F6} lon:{lon:F6}");
            }
        }
    }

    private void OnDestroy()
    {
        if (userLocation != null)
            userLocation.OnLocationChanged -= CheckArrival;
    }

    private void Update()
    {
        if (!isInside) return;

        //only spawn once per XR entry
        if (wolfInstance != null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1))
            return;

        var selectState = arInteractor.logicalSelectState;
        if (!selectState.wasCompletedThisFrame)
            return;

        TrySpawnAtCurrentHit();
    }

    private void CheckArrival(GeoPoint userPoint)
    {
        if (Time.time < nextAllowedSwitchTime) return;

        var markers2D = map.marker2DManager.items;
        if (markers2D == null || markers2D.Count == 0) return;

        double nearestMeters = double.MaxValue;
        Marker2D nearestMarker = null;

        foreach (var m in markers2D)
        {
            double dMeters = userPoint.Distance(m.location) * 1000.0;
            if (dMeters < nearestMeters)
            {
                nearestMeters = dMeters;
                nearestMarker = m;
            }
        }

        if (nearestMarker == null) return;

        bool desiredInside = isInside;

        if (!isInside)
        {
            if (nearestMeters <= enterDistanceMeters) desiredInside = true;
        }
        else
        {
            if (nearestMeters >= exitDistanceMeters) desiredInside = false;
        }

        if (desiredInside == isInside) return;

        isInside = desiredInside;
        nextAllowedSwitchTime = Time.time + switchCooldownSeconds;

        //switch views
        xrOrigin.SetActive(isInside);
        mapCamera.enabled = !isInside;

        if (isInside)
        {
            pendingMarkerLabel = nearestMarker.label ?? "";
        }
        else
        {
            pendingMarkerLabel = null;
            lastPlayedLabel = null;

            if (audioSource)
                audioSource.Stop();

            //leaving XR: remove wolf and reset UI
            if (wolfInstance != null)
            {
                Destroy(wolfInstance);
                wolfInstance = null;
                wolfAnimator = null;
            }
        }

        RefreshUIState();

        Debug.Log($"MapControl: Inside={isInside} distance={nearestMeters:0.0}m nearestLabel=\"{nearestMarker.label}\"");
    }

    private void TrySpawnAtCurrentHit()
    {
        if (!arInteractor.TryGetCurrentARRaycastHit(out var arHit))
            return;

        if (!(arHit.trackable is ARPlane plane))
            return;

        if (requireHorizontalUpSurface && plane.alignment != PlaneAlignment.HorizontalUp)
            return;

        wolfInstance = Instantiate(wolfPrefab, arHit.pose.position, Quaternion.identity);

        Vector3 toCam = xrCamera.position - wolfInstance.transform.position;
        toCam = Vector3.ProjectOnPlane(toCam, Vector3.up);
        if (toCam.sqrMagnitude > 0.0001f)
            wolfInstance.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);

        wolfAnimator = wolfInstance.GetComponentInChildren<Animator>(true);

        RefreshUIState();

        //play audio only after the wolf is spawned 
        TryPlayAudioForLabel(pendingMarkerLabel);
    }

    private void RefreshUIState()
    {
        if (spawnInstructionUI)
            spawnInstructionUI.SetActive(isInside && wolfInstance == null);

        if (wolfActionButton)
            wolfActionButton.SetActive(isInside && wolfInstance != null);
    }

    private void TryPlayAudioForLabel(string label)
    {
        if (!audioSource) return;
        if (string.IsNullOrWhiteSpace(label)) return;

        if (lastPlayedLabel == label) return;

        for (int i = 0; i < markerAudio.Count; i++)
        {
            var entry = markerAudio[i];
            if (entry == null) continue;

            if (entry.label == label && entry.clip != null)
            {
                audioSource.Stop();
                audioSource.clip = entry.clip;
                audioSource.Play();

                lastPlayedLabel = label;
                Debug.Log($"MapControl: Playing audio for marker label \"{label}\"");
                return;
            }
        }

        Debug.Log($"MapControl: No audio mapping found for label \"{label}\"");
    }

    public void PlayWolfAnimation()
    {
        if (!isInside) return;
        if (wolfInstance == null) return;

        if (wolfAnimator == null)
        {
            wolfAnimator = wolfInstance.GetComponentInChildren<Animator>(true);
            if (wolfAnimator == null)
            {
                Debug.LogWarning("MapControl: Wolf has no Animator.");
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(wolfAnimatorTriggerName))
        {
            Debug.LogWarning("MapControl: wolfAnimatorTriggerName is empty.");
            return;
        }

        wolfAnimator.SetTrigger(wolfAnimatorTriggerName);
    }
}