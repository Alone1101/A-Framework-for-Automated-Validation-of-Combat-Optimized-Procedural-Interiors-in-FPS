using UnityEngine;

public class ValidationManager : MonoBehaviour
{
    public static ValidationManager Instance;

    [Header("Global Toggles")]
    public bool showCoverLines = false;
    public bool showExclusionZones = false;
    public bool showHeatmap = false;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }
}