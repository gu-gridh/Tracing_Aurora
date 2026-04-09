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
    public float enterDistanceMeters = 10f;
    public float exitDistanceMeters = 15f;
    public float switchCooldownSeconds = 2f;

    [Header("View Switching")]
    public Camera xrOriginCamera;
    public Camera mapCamera;

    [Tooltip("Shown when the player is near a marker and can switch into XR.")]
    public GameObject enterXRButton;

    [Tooltip("Shown while the player is in XR so they can return to the map at any time.")]
    public GameObject closeXRButton;

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
    private bool isInXRView;

    private GameObject wolfInstance;
    private Animator wolfAnimator;

    private string pendingMarkerLabel;
    private string lastPlayedLabel;
    private bool isWaitingForAudioToFinish;
    private bool hasAudioFinishedForCurrentEntry = true;

    private void Start()
    {
        userLocation = UserLocation.instance;

        if (!userLocation || !map || !xrOriginCamera || !mapCamera || !arInteractor || !xrCamera || !wolfPrefab)
        {
            Debug.LogError("MapControl: Missing required references.");
            enabled = false;
            return;
        }

        userLocation.OnLocationChanged += CheckArrival;

        xrOriginCamera.enabled = false;
        mapCamera.enabled = true;

        RefreshUIState();
    }

    private void OnDestroy()
    {
        if (userLocation != null)
            userLocation.OnLocationChanged -= CheckArrival;
    }

    private void Update()
    {
        UpdateAudioCompletionState();

        if (!isInXRView) return;

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
        if (!TryGetNearestMarker(userPoint, out Marker2D nearestMarker, out double nearestMeters))
            return;

        bool wasInside = isInside;
        string previousPendingMarkerLabel = pendingMarkerLabel;

        isInside = nearestMeters <= enterDistanceMeters;

        if (isInside)
        {
            pendingMarkerLabel = nearestMarker.label ?? "";
        }
        else
        {
            if (!isInXRView)
            {
                pendingMarkerLabel = null;
                lastPlayedLabel = null;
            }
        }

        if (wasInside == isInside && previousPendingMarkerLabel == pendingMarkerLabel)
            return;

        RefreshUIState();
    }

    private bool TryGetNearestMarker(GeoPoint userPoint, out Marker2D nearestMarker, out double nearestMeters)
    {
        var markers2D = map.marker2DManager.items;
        nearestMeters = double.MaxValue;
        nearestMarker = null;

        if (markers2D == null || markers2D.Count == 0)
            return false;

        foreach (var marker in markers2D)
        {
            if (marker == null || !marker.enabled) continue;
            if (string.IsNullOrWhiteSpace(marker.label)) continue;

            double distanceMeters = userPoint.Distance(marker.location) * 1000.0;
            if (distanceMeters < nearestMeters)
            {
                nearestMeters = distanceMeters;
                nearestMarker = marker;
            }
        }

        return nearestMarker != null;
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
        if (enterXRButton)
            enterXRButton.SetActive(isInside && !isInXRView);

        if (closeXRButton)
            closeXRButton.SetActive(isInXRView);

        if (spawnInstructionUI)
            spawnInstructionUI.SetActive(isInXRView && wolfInstance == null);

        if (wolfActionButton)
            wolfActionButton.SetActive(isInXRView && wolfInstance != null);
    }

    public void EnterXRView()
    {
        if (!isInside)
            return;

        GeoPoint currentPoint = userLocation != null ? userLocation.location : GeoPoint.zero;
        if (userLocation != null && TryGetNearestMarker(currentPoint, out Marker2D nearestMarker, out _))
            pendingMarkerLabel = nearestMarker.label ?? pendingMarkerLabel;

        SetViewState(true);
    }

    public void ExitXRView()
    {
        SetViewState(false);
    }

    private void SetViewState(bool showXR)
    {
        if (isInXRView == showXR) return;

        isInXRView = showXR;
        xrOriginCamera.enabled = isInXRView;
        mapCamera.enabled = !isInXRView;

        if (isInXRView)
        {
            hasAudioFinishedForCurrentEntry = false;
            isWaitingForAudioToFinish = false;
        }

        if (!isInXRView)
        {
            lastPlayedLabel = null;
            hasAudioFinishedForCurrentEntry = true;
            isWaitingForAudioToFinish = false;

            if (audioSource)
                audioSource.Stop();

            if (wolfInstance != null)
            {
                Destroy(wolfInstance);
                wolfInstance = null;
                wolfAnimator = null;
            }
        }

        RefreshUIState();
    }

    private void UpdateAudioCompletionState()
    {
        if (!isInXRView) return;
        if (!isWaitingForAudioToFinish) return;
        if (audioSource != null && audioSource.isPlaying) return;

        isWaitingForAudioToFinish = false;
        hasAudioFinishedForCurrentEntry = true;
        RefreshUIState();
    }

    private void TryPlayAudioForLabel(string label)
    {
        if (!audioSource)
        {
            hasAudioFinishedForCurrentEntry = true;
            isWaitingForAudioToFinish = false;
            RefreshUIState();
            return;
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            hasAudioFinishedForCurrentEntry = true;
            isWaitingForAudioToFinish = false;
            RefreshUIState();
            return;
        }

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
                hasAudioFinishedForCurrentEntry = false;
                isWaitingForAudioToFinish = true;
                RefreshUIState();
                return;
            }
        }

        hasAudioFinishedForCurrentEntry = true;
        isWaitingForAudioToFinish = false;
        RefreshUIState();
    }

    public void PlayWolfAnimation()
    {
        if (!isInXRView) return;
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
