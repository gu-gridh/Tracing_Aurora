/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of get place predictions from Google Autocomplete API.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "FindAutocompleteExample")]
    public class FindAutocompleteExample : MonoBehaviour
    {
        private void Start()
        {
            // Makes a request to Google Places Autocomplete API.
            new GooglePlacesAutocompleteRequest("Los ang")
                .HandleResult(OnAutocompleteResult) // Subscribe to the event of receiving results.
                .Send(); // Send the request.
        }

        /// <summary>
        /// This method is called when a response is received.
        /// </summary>
        /// <param name="results">Array of results</param>
        private void OnAutocompleteResult(GooglePlacesAutocompleteResult[] results)
        {
            // If there is no result
            if (results == null || results.Length == 0)
            {
                Debug.Log("No results");
                return;
            }

            // Log description of each result.
            foreach (GooglePlacesAutocompleteResult result in results)
            {
                Debug.Log(result.description);
            }
            
            // Get location of the first result.
            new GooglePlaceDetailsRequest{place_id = results[0].place_id}
                .HandleResult(OnPlaceDetailsResult) // Subscribe to the event of receiving place details.
                .Send(); // Send the request.
        }

        /// <summary>
        /// This method is called when place details are received.
        /// </summary>
        /// <param name="result">Place details result</param>
        private void OnPlaceDetailsResult(GooglePlaceDetailsResult result)
        {
            // If there is no result
            if (result == null)
            {
                Debug.Log("No place details found");
                return;
            }

            // Get the location.
            Debug.Log($"Place location: {result.location.ToString()}");
        }
    }
}