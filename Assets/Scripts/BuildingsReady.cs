using UnityEngine;
using System.Collections;
using OnlineMaps;

[DisallowMultipleComponent]
public class BuildingsReady : MonoBehaviour
{
    public float timeoutSeconds = 15f;

    private Buildings b;
    private Map map;

    private void Awake()
    {
        b = GetComponent<Buildings>();
        map = GetComponent<Map>();

        if (b == null || map == null)
        {
            enabled = false;
            return;
        }
    }

    private IEnumerator Start()
    {
        //disable Buildings until map is fully initialized
        b.enabled = false;

        float startTime = Time.time;

        while (true)
        {
            bool viewReady = map.view != null;
            bool controlReady = map.control is ControlBaseDynamicMesh;

            if (viewReady && controlReady)
                break;

            if (timeoutSeconds > 0 && Time.time - startTime > timeoutSeconds)
                break;

            yield return null;
        }

        yield return null;

        b.enabled = true;
    }
}