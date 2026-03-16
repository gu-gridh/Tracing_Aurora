/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Linq;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Route Emulator
    /// </summary>
    [RequireComponent(typeof(UserLocation))]
    [Plugin("Route Emulator")]
    public class RouteEmulator : MonoBehaviour
    {
        #region Events

        /// <summary>
        /// Called when the position changes
        /// </summary>
        public event Action<GeoPoint> OnLocationChanged;

        /// <summary>
        /// Called when the route starts
        /// </summary>
        public event Action OnRouteStarted;

        /// <summary>
        /// Called when the route is completed
        /// </summary>
        public event Action OnRouteCompleted;

        #endregion
        
        #region Fields

        [SerializeField]
        private RouteSourceType routeSourceType = RouteSourceType.Coordinates;

        [SerializeField]
        private double speed = 60;

        [SerializeField]
        private SpeedUnit speedUnit = SpeedUnit.KilometersPerHour;

        [SerializeField]
        private List<GeoPoint> coordinates = new List<GeoPoint>();
        
        [SerializeField]
        private RoutingPointType originType = RoutingPointType.Location;
        
        [SerializeField]
        private string originAddress;
        
        [SerializeField]
        private GeoPoint originLocation;
        
        [SerializeField]
        private RoutingPointType destinationType = RoutingPointType.Location;
        
        [SerializeField]
        private string destinationAddress;
        
        [SerializeField]
        private GeoPoint destinationLocation;
        
        [SerializeField]
        private bool isPlaying;
        #endregion

        #region Properties

        /// <summary>
        /// Current speed in meters per second
        /// </summary>
        public double currentSpeedMetersPerSecond => GetSpeedInMetersPerSecond();

        /// <summary>
        /// Current position on the route (0-1 progress)
        /// </summary>
        public double currentProgress { get; private set; }

        /// <summary>
        /// Current position coordinates
        /// </summary>
        public GeoPoint currentLocation { get; private set; }

        /// <summary>
        /// Total route distance in meters
        /// </summary>
        public double totalDistance { get; private set; }

        /// <summary>
        /// Elapsed time in seconds
        /// </summary>
        public double elapsedTime { get; private set; }

        #endregion

        #region Private Variables

        private List<GeoPoint> routePoints = new List<GeoPoint>();
        private int currentSegmentIndex = 0;
        private double segmentProgress = 0;
        private bool isPaused = false;
        private UserLocation userLocation;

        #endregion

        #region Unity Methods

        private void Start()
        {
            if (routeSourceType == RouteSourceType.GoogleRoutingAPI && !KeyManager.hasGoogleMaps)
            {
                Debug.LogError("RouteEmulator: Google Maps API key is not set in Key Manager.");
                Destroy(this);
                return;
            }
            
            userLocation = GetComponent<UserLocation>();
        }

        private void Update()
        {
            if (!isPlaying || isPaused) return;

            UpdateRoute(Time.deltaTime);
        }

        #endregion

        #region Public Methods

        public double GetSpeedInKmPerHour()
        {
            switch (speedUnit)
            {
                case SpeedUnit.KilometersPerHour:
                    return speed;

                case SpeedUnit.MilesPerHour:
                    return speed * 1.60934;

                case SpeedUnit.MetersPerSecond:
                    return speed * 3.6;

                default:
                    return speed;
            }
        }

        /// <summary>
        /// Convert speed to meters per second
        /// </summary>
        /// <returns>Speed in m/s</returns>
        public double GetSpeedInMetersPerSecond()
        {
            switch (speedUnit)
            {
                case SpeedUnit.KilometersPerHour:
                    return speed / 3.6;

                case SpeedUnit.MilesPerHour:
                    return speed / 2.23694;

                case SpeedUnit.MetersPerSecond:
                default:
                    return speed;
            }
        }

        /// <summary>
        /// Get the next position after the specified time delta
        /// </summary>
        /// <param name="timeDelta">Time in seconds</param>
        /// <returns>New position</returns>
        public GeoPoint GetNextPosition(double timeDelta)
        {
            if (routePoints.Count < 2) return currentLocation;

            double speedMPS = GetSpeedInMetersPerSecond();
            double distance = speedMPS * timeDelta;

            return MoveAlongRoute(distance);
        }

        /// <summary>
        /// Check if the route emulation is paused
        /// </summary>
        /// <returns>True if paused</returns>
        public bool IsPaused() => isPaused;

        /// <summary>
        /// Pause the route emulation
        /// </summary>
        public void Pause() => isPaused = true;

        /// <summary>
        /// Start the route emulation
        /// </summary>
        public void Play()
        {
            if (isPlaying && !isPaused) return;

            if (isPaused)
            {
                isPaused = false;
                return;
            }

            // Initialize route
            if (routeSourceType == RouteSourceType.Coordinates)
            {
                InitializeRouteFromCoordinates();
            }
            else if (routeSourceType == RouteSourceType.GoogleRoutingAPI/* && googleRoutingRequest != null*/)
            {
                InitializeRouteFromGoogleAPI();
            }

            isPlaying = true;
            isPaused = false;
            elapsedTime = 0;
            currentSegmentIndex = 0;
            segmentProgress = 0;

            if (OnRouteStarted != null)
            {
                OnRouteStarted.Invoke();
            }
        }

        /// <summary>
        /// Reset the route to the beginning
        /// </summary>
        public void Reset()
        {
            Stop();
            currentSegmentIndex = 0;
            segmentProgress = 0;
            currentProgress = 0;
            elapsedTime = 0;

            if (routePoints.Count > 0)
            {
                currentLocation = routePoints[0];
            }
        }

        /// <summary>
        /// Stop the route emulation
        /// </summary>
        public void Stop()
        {
            isPlaying = false;
            isPaused = false;

            if (OnRouteCompleted != null)
            {
                OnRouteCompleted.Invoke();
            }
        }
        
        #endregion

        #region Private Methods

        private double CalculateTotalProgress()
        {
            if (totalDistance <= 0) return 0;

            double completedDistance = 0;
            for (int i = 0; i < currentSegmentIndex; i++)
            {
                completedDistance += routePoints[i].Distance(routePoints[i + 1]) * 1000;
            }

            if (currentSegmentIndex < routePoints.Count - 1)
            {
                double segmentDistance = routePoints[currentSegmentIndex].Distance(routePoints[currentSegmentIndex + 1]) * 1000;
                completedDistance += segmentProgress * segmentDistance;
            }

            return completedDistance / totalDistance;
        }

        private void CalculateTotalDistance()
        {
            totalDistance = 0;
            for (int i = 0; i < routePoints.Count - 1; i++)
            {
                totalDistance += routePoints[i].Distance(routePoints[i + 1]) * 1000;
            }
        }

        private void InitializeRouteFromCoordinates()
        {
            routePoints.Clear();
            routePoints.AddRange(coordinates);
            CalculateTotalDistance();
            
            if (routePoints.Count > 0)
            {
                currentLocation = routePoints[0];
            }
        }

        private void InitializeRouteFromGoogleAPI()
        {
            if (routePoints.Count > 0) return;

            new GoogleRoutingRequest(
                    originType == RoutingPointType.Address ? originAddress : originLocation,
                    destinationType == RoutingPointType.Address ? destinationAddress : destinationLocation
                ).HandleResult(OnRequestComplete)
                .Send();
        }

        private void OnRequestComplete(GoogleRoutingResult result)
        {
            // If there are no routes, return
            if (result.routes.Length == 0)
            {
                Debug.Log("Can't find route");
                return;
            }

            GoogleRoutingResult.Route route = result.routes[0];
            if (route == null)
            {
                Debug.Log("Can't find route");
                return;
            }
            
            routePoints.Clear();
            routePoints = result.routes[0].polyline.points.ToList();
            
            CalculateTotalDistance();

            if (routePoints.Count > 0)
            {
                currentLocation = routePoints[0];
            }
        }

        private GeoPoint MoveAlongRoute(double distance)
        {
            if (routePoints.Count < 2) return currentLocation;

            while (currentSegmentIndex < routePoints.Count - 1)
            {
                GeoPoint start = routePoints[currentSegmentIndex];
                GeoPoint end = routePoints[currentSegmentIndex + 1];

                double segmentDistance = start.Distance(end) * 1000; // Convert to meters
                double remainingDistance = (1 - segmentProgress) * segmentDistance;

                if (distance < remainingDistance)
                {
                    // Still within current segment
                    segmentProgress += distance / segmentDistance;
                    currentProgress = CalculateTotalProgress();
                    return start.Lerp(end, segmentProgress);
                }
                else
                {
                    // Move to next segment
                    distance -= remainingDistance;
                    currentSegmentIndex++;
                    segmentProgress = 0;

                    if (currentSegmentIndex >= routePoints.Count - 1)
                    {
                        // Reached the end
                        currentProgress = 1;
                        return routePoints[routePoints.Count - 1];
                    }
                }
            }

            return currentLocation;
        }

        private void UpdateRoute(double timeDelta)
        {
            if (routePoints.Count < 2) return;
            double speedMPS = GetSpeedInMetersPerSecond();
            double distance = speedMPS * timeDelta;

            elapsedTime += timeDelta;
            currentLocation = MoveAlongRoute(distance);
            if (userLocation) userLocation.emulatedLocation = currentLocation;

            if (OnLocationChanged != null)
            {
                OnLocationChanged.Invoke(currentLocation);
            }

            // Check if route is completed
            if (currentSegmentIndex >= routePoints.Count - 1 && segmentProgress >= 1)
            {
                Stop();
            }
        }

        #endregion

        #region Nested Types

        public enum RouteSourceType
        {
            Coordinates,
            GoogleRoutingAPI
        }
        
        public enum SpeedUnit
        {
            KilometersPerHour,
            MilesPerHour,
            MetersPerSecond
        }
        
        public enum RoutingPointType
        {
            Address,
            Location
        }

        #endregion
    }
}
