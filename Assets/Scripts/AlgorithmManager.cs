using UnityEngine;

/// <summary>
/// AlgorithmManager: starts/stops the Flock/Maze/Pathfinding algorithms using either
/// scene objects or prefabs assigned in the inspector. This implementation avoids
/// calling instance methods as if they were static and uses safe fallbacks so your
/// existing algorithm scripts will be invoked without requiring invasive changes.
/// 
/// Instructions:
/// - Assign flockingObject / mazeObject / pathfindingObject (either scene objects or prefab assets) in the Inspector.
/// - Prefer implementing IAlgorithm on your algorithm controllers (StartAlgorithm / StopAlgorithm).
/// - If you cannot change the algorithm scripts, ensure they expose a public method
///   named one of: "StartAlgorithm", "StartFlocking", "GenerateMaze", "StartPathfinding" (or the manager will use SendMessage fallback).
/// </summary>
public class AlgorithmManager : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private MobileOrbitCamera orbitCamera; // optional, assign in Inspector

    [Header("Algorithm objects (scene objects or prefabs)")]
    [Tooltip("Assign either a scene GameObject or a prefab for the flocking algorithm.")]
    [SerializeField] private GameObject flockingObject;
    [Tooltip("Assign either a scene GameObject or a prefab for the maze algorithm.")]
    [SerializeField] private GameObject mazeObject;
    [Tooltip("Assign either a scene GameObject or a prefab for the pathfinding algorithm.")]
    [SerializeField] private GameObject pathfindingObject;

    // active instance and metadata
    private GameObject currentInstance;
    private bool currentInstanceWasInstantiated = false;
    private IAlgorithm currentAlgoComponent;

    public enum AlgorithmType { None, Flock, Maze, Pathfinding }
    private AlgorithmType current = AlgorithmType.None; // start with None so pressing "Flock" at launch will run it

    void Awake()
    {
        if (orbitCamera == null)
        {
            var mainCam = Camera.main;
            if (mainCam != null)
                orbitCamera = mainCam.GetComponent<MobileOrbitCamera>();
        }
    }

    // Public entry used by UIManager
    public void SwitchToAlgorithm(AlgorithmType type)
    {
        if (type == current) return;

        // stop current only if something was active
        if (current != AlgorithmType.None)
            StopAlgorithm(current);

        StartAlgorithm(type);

        orbitCamera?.ResetTouchState();

        current = type;
    }

    // Backwards-compatible wrappers
    public void SwitchToFlocking()     => SwitchToAlgorithm(AlgorithmType.Flock);
    public void SwitchToMaze()         => SwitchToAlgorithm(AlgorithmType.Maze);
    public void SwitchToPathfinding()  => SwitchToAlgorithm(AlgorithmType.Pathfinding);

    void StartAlgorithm(AlgorithmType type)
    {
        switch (type)
        {
            case AlgorithmType.Flock: StartFlock(); break;
            case AlgorithmType.Maze: StartMaze(); break;
            case AlgorithmType.Pathfinding: StartPathfinding(); break;
        }
    }

    void StopAlgorithm(AlgorithmType type)
    {
        // if nothing active, nothing to do
        if (currentInstance == null) return;

        // 1) try to call component stop hook
        try
        {
            if (currentAlgoComponent != null)
            {
                currentAlgoComponent.StopAlgorithm();
            }
            else
            {
                // If no IAlgorithm, try sending name-based messages (safe fallback)
                currentInstance.SendMessage("StopAlgorithm", SendMessageOptions.DontRequireReceiver);
                currentInstance.SendMessage("StopFlocking", SendMessageOptions.DontRequireReceiver);
                currentInstance.SendMessage("StopPathfinding", SendMessageOptions.DontRequireReceiver);
                currentInstance.SendMessage("StopMaze", SendMessageOptions.DontRequireReceiver);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"AlgorithmManager: exception when stopping algorithm: {ex}");
        }

        if (currentInstanceWasInstantiated)
            Destroy(currentInstance);
        else
            currentInstance.SetActive(false);

        currentInstance = null;
        currentAlgoComponent = null;
        currentInstanceWasInstantiated = false;
    }

    // Concrete start implementations
    void StartFlock()
    {
        Debug.Log("AlgorithmManager: Starting Flock algorithm...");

        GameObject source = ResolveObjectForType(flockingObject, "FlockManager");
        if (source == null)
        {
            Debug.LogWarning("No flocking object/prefab found. Assign flockingObject or add a GameObject named 'FlockManager'.");
            return;
        }

        InstantiateOrActivate(source, AlgorithmType.Flock);
    }

    void StartMaze()
    {
        Debug.Log("AlgorithmManager: Starting Maze algorithm...");

        GameObject source = ResolveObjectForType(mazeObject, "MazeManager");
        if (source == null)
        {
            Debug.LogWarning("No maze object/prefab found. Assign mazeObject or add a GameObject named 'MazeManager'.");
            return;
        }

        InstantiateOrActivate(source, AlgorithmType.Maze);
    }

    void StartPathfinding()
    {
        Debug.Log("AlgorithmManager: Starting Pathfinding algorithm...");

        GameObject source = ResolveObjectForType(pathfindingObject, "PathfindingManager");
        if (source == null)
        {
            Debug.LogWarning("No pathfinding object/prefab found. Assign pathfindingObject or add a GameObject named 'PathfindingManager'.");
            return;
        }

        InstantiateOrActivate(source, AlgorithmType.Pathfinding);
    }

    // Helpers
    private GameObject ResolveObjectForType(GameObject inspectorAssigned, string fallbackName)
    {
        if (inspectorAssigned != null) return inspectorAssigned;

        var found = GameObject.Find(fallbackName);
        return found;
    }

    private void InstantiateOrActivate(GameObject source, AlgorithmType type)
    {
        if (source == null) return;

        bool sourceIsSceneObject = source.scene.IsValid();

        if (sourceIsSceneObject)
        {
            currentInstanceWasInstantiated = false;
            currentInstance = source;
            currentInstance.SetActive(true);
        }
        else
        {
            currentInstanceWasInstantiated = true;
            currentInstance = Instantiate(source);
            currentInstance.name = source.name + "_Instance";
        }

        // Try IAlgorithm first
        currentAlgoComponent = FindIAlgorithmComponent(currentInstance);
        if (currentAlgoComponent != null)
        {
            try { currentAlgoComponent.StartAlgorithm(); }
            catch (System.Exception ex) { Debug.LogWarning($"AlgorithmManager: exception in IAlgorithm.StartAlgorithm: {ex}"); }
        }
        else
        {
            // No IAlgorithm: use safe SendMessage fallbacks depending on algorithm type
            switch (type)
            {
                case AlgorithmType.Flock:
                    currentInstance.SendMessage("StartAlgorithm", SendMessageOptions.DontRequireReceiver);
                    currentInstance.SendMessage("StartFlocking", SendMessageOptions.DontRequireReceiver);
                    break;
                case AlgorithmType.Maze:
                    currentInstance.SendMessage("StartAlgorithm", SendMessageOptions.DontRequireReceiver);
                    currentInstance.SendMessage("GenerateMaze", SendMessageOptions.DontRequireReceiver);
                    currentInstance.SendMessage("StartMaze", SendMessageOptions.DontRequireReceiver);
                    break;
                case AlgorithmType.Pathfinding:
                    currentInstance.SendMessage("StartAlgorithm", SendMessageOptions.DontRequireReceiver);
                    currentInstance.SendMessage("StartPathfinding", SendMessageOptions.DontRequireReceiver);
                    break;
            }
        }

        // Make camera orbit this instance by default (optional)
        if (orbitCamera != null)
        {
            orbitCamera.SetTarget(currentInstance.transform);
        }
    }

    private IAlgorithm FindIAlgorithmComponent(GameObject go)
    {
        if (go == null) return null;

        var mbs = go.GetComponents<MonoBehaviour>();
        foreach (var mb in mbs)
            if (mb is IAlgorithm a) return a;

        var childMBs = go.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in childMBs)
            if (mb is IAlgorithm a) return a;

        return null;
    }

    // Allow external code to stop what's running quickly
    public void StopCurrentAlgorithm()
    {
        StopAlgorithm(current);
        current = AlgorithmType.Flock;
    }

    public GameObject GetActiveInstance() => currentInstance;
}