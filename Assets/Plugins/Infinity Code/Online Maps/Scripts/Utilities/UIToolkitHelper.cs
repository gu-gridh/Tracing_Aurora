/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace OnlineMaps
{
    /// <summary>
    /// Helper class for working with Unity UI Toolkit elements.
    /// </summary>
    public static class UIToolkitHelper
    {
        private static UIDocument document;
        private static bool isMissed = false;
        private static IPanel panel;
        private static List<VisualElement> picked = new List<VisualElement>();
        private static PanelSettings settings;

        private static Vector2 ConvertForScaleWithScreenSize(Vector2 mousePosition)
        {
            Vector2 referenceResolution = settings.referenceResolution;
            Vector2 currentResolution = new Vector2(Screen.width, Screen.height);
            PanelScreenMatchMode screenMatchMode = settings.screenMatchMode;

            float scale = 1;
            switch (screenMatchMode)
            {
                case PanelScreenMatchMode.MatchWidthOrHeight:
                    float logWidth = Mathf.Log(currentResolution.x / referenceResolution.x, 2);
                    float logHeight = Mathf.Log(currentResolution.y / referenceResolution.y, 2);
                    float logWeightedAverage = Mathf.Lerp(logWidth, logHeight, settings.match);
                    scale = Mathf.Pow(2, logWeightedAverage);
                    break;

                case PanelScreenMatchMode.Expand:
                case PanelScreenMatchMode.Shrink:
                    scale = Mathf.Min(currentResolution.x / referenceResolution.x, currentResolution.y / referenceResolution.y);
                    break;
            }

            float uiX = mousePosition.x / scale;
            float uiY = (currentResolution.y - mousePosition.y) / scale;

            return new Vector2(uiX, uiY);
        }

        private static Vector2 ConvertMousePositionForScaleMode(Vector2 mousePosition)
        {
            Vector2 currentResolution = new Vector2(Screen.width, Screen.height);

            if (settings.scaleMode == PanelScaleMode.ScaleWithScreenSize)
            {
                return ConvertForScaleWithScreenSize(mousePosition);
            }
            return new Vector2(mousePosition.x, currentResolution.y - mousePosition.y);
        }

        /// <summary>
        /// Returns the first interactive UI Toolkit element under the specified mouse position.
        /// </summary>
        public static VisualElement GetInteractiveElement(Vector2 mousePosition)
        {
            if (panel == null)
            {
                if (!isMissed)
                {
                    document = Compatibility.FindObjectOfType<UIDocument>();
                    isMissed = !document;
                    if (isMissed) return null;

                    settings = document.panelSettings;
                    panel = document.rootVisualElement.panel;
                }
                else return null;
            }
            
            Vector2 screenPosition = ConvertMousePositionForScaleMode(mousePosition);

            panel.PickAll(screenPosition, picked);
            return picked.FirstOrDefault(IsInteractiveElement);
        }

        /// <summary>
        /// Checks if the cursor is currently over an interactive UI Toolkit element at the specified mouse position.
        /// </summary>
        /// <param name="mousePosition">The position of the mouse cursor in screen coordinates.</param>
        /// <returns>True if the cursor is over an interactive element; otherwise, false.</returns>
        public static bool IsCursorOverInteractiveElement(Vector2 mousePosition)
        {
            return GetInteractiveElement(mousePosition) != null;
        }

        private static bool IsInteractiveElement(VisualElement element)
        {
            return element is Button ||
                   element is TextField ||
                   element is Toggle ||
                   element is Slider ||
                   element is ScrollView ||
                   element is Scroller ||
                   element is DropdownField ||
                   element is MinMaxSlider ||
                   element.focusable;
        }
    }
}