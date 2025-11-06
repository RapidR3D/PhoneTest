using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI manager that wires UI buttons to AlgorithmManager switch methods.
/// This matches the state before we started changing camera behavior: it simply
/// forwards button clicks to the AlgorithmManager wrapper methods (SwitchToFlocking, SwitchToMaze, SwitchToPathfinding).
/// 
/// Usage:
/// - Assign the AlgorithmManager reference in the inspector (or leave null to auto-find).
/// - Assign the three Buttons in the inspector (or hook them up in the Inspector via OnClick()).
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AlgorithmManager algorithmManager;
    [Header("Buttons (optional)")]
    [SerializeField] private Button flockButton;
    [SerializeField] private Button mazeButton;
    [SerializeField] private Button pathfindingButton;

    void Awake()
    {
        // Try to auto-find AlgorithmManager if not set
        if (algorithmManager == null)
        {
            algorithmManager = FindObjectOfType<AlgorithmManager>();
        }

        // Hook up button callbacks if buttons were assigned in the inspector
        if (flockButton != null)
            flockButton.onClick.AddListener(OnFlockButton);

        if (mazeButton != null)
            mazeButton.onClick.AddListener(OnMazeButton);

        if (pathfindingButton != null)
            pathfindingButton.onClick.AddListener(OnPathfindingButton);
    }

    void OnDestroy()
    {
        // Remove listeners to avoid leaks when switching scenes / playmode
        if (flockButton != null)
            flockButton.onClick.RemoveListener(OnFlockButton);
        if (mazeButton != null)
            mazeButton.onClick.RemoveListener(OnMazeButton);
        if (pathfindingButton != null)
            pathfindingButton.onClick.RemoveListener(OnPathfindingButton);
    }

    // These methods match the AlgorithmManager wrappers that existed before the camera fixes.
    public void OnFlockButton()
    {
        if (algorithmManager != null)
            algorithmManager.SwitchToFlocking();
        else
            Debug.LogWarning("UIManager: AlgorithmManager reference not set.");
    }

    public void OnMazeButton()
    {
        if (algorithmManager != null)
            algorithmManager.SwitchToMaze();
        else
            Debug.LogWarning("UIManager: AlgorithmManager reference not set.");
    }

    public void OnPathfindingButton()
    {
        if (algorithmManager != null)
            algorithmManager.SwitchToPathfinding();
        else
            Debug.LogWarning("UIManager: AlgorithmManager reference not set.");
    }
}