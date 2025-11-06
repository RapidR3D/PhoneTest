using System.Collections.Generic;
using UnityEngine;

// Simple grid A* demo. Visualizes path and moves a single agent along it.
public class PathfindingAlgorithm : MonoBehaviour, IAlgorithm
{
    public int width = 20;
    public int height = 20;
    public float cellSize = 1f;
    public GameObject floorTilePrefab;
    public GameObject wallPrefab;
    public GameObject agentPrefab;
    public Transform markerPrefab; // optional marker for path nodes

    GridNode[,] grid;
    GameObject gridHolder;
    GameObject agent;
    List<GridNode> currentPath;

    public void StartAlgorithm()
    {
        gridHolder = new GameObject("GridHolder");
        gridHolder.transform.parent = transform;
        CreateGrid();
        // find simple path from left-bottom to right-top
        var start = grid[0, 0];
        var goal = grid[width - 1, height - 1];
        currentPath = FindPath(start, goal);
        SpawnAgentAndFollowPath();
    }

    public void StopAlgorithm()
    {
        if (gridHolder != null) Destroy(gridHolder);
        if (agent != null) Destroy(agent);
        currentPath = null;
    }

    void CreateGrid()
    {
        grid = new GridNode[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Create tile
                grid[x, y] = new GridNode(x, y, true);
                if (floorTilePrefab != null)
                {
                    var t = Instantiate(floorTilePrefab, grid[x, y].WorldPosition(cellSize, transform.position), Quaternion.identity, gridHolder.transform);
                    t.transform.localScale = Vector3.one * cellSize * 0.9f;
                }

                // Optional random walls for demo
                if (Random.value < 0.15f && !(x == 0 && y == 0) && !(x == width - 1 && y == height - 1))
                {
                    grid[x, y].walkable = false;
                    if (wallPrefab != null)
                    {
                        var w = Instantiate(wallPrefab, grid[x, y].WorldPosition(cellSize, transform.position), Quaternion.identity, gridHolder.transform);
                        w.transform.localScale = new Vector3(cellSize, 1f, cellSize);
                    }
                }
            }
        }
    }

    void SpawnAgentAndFollowPath()
    {
        if (agentPrefab != null && currentPath != null && currentPath.Count > 0)
        {
            agent = Instantiate(agentPrefab, currentPath[0].WorldPosition(cellSize, transform.position), Quaternion.identity, transform);
            agent.AddComponent<PathFollower>().Initialize(currentPath, cellSize, transform.position);
        }

        if (markerPrefab != null && currentPath != null)
        {
            foreach (var n in currentPath)
            {
                Instantiate(markerPrefab, n.WorldPosition(cellSize, transform.position) + Vector3.up * 0.1f, Quaternion.identity, gridHolder.transform);
            }
        }
    }

    List<GridNode> FindPath(GridNode start, GridNode goal)
    {
        var open = new List<GridNode>();
        var closed = new HashSet<GridNode>();
        open.Add(start);

        start.gCost = 0;
        start.hCost = Heuristic(start, goal);

        while (open.Count > 0)
        {
            var current = open[0];
            for (int i = 1; i < open.Count; i++)
                if (open[i].fCost < current.fCost || (open[i].fCost == current.fCost && open[i].hCost < current.hCost))
                    current = open[i];

            open.Remove(current);
            closed.Add(current);

            if (current == goal)
                return ReconstructPath(goal);

            foreach (var nb in GetNeighbors(current))
            {
                if (!nb.walkable || closed.Contains(nb)) continue;

                float tentativeG = current.gCost + Vector2.Distance(new Vector2(current.x, current.y), new Vector2(nb.x, nb.y));
                if (!open.Contains(nb) || tentativeG < nb.gCost)
                {
                    nb.gCost = tentativeG;
                    nb.hCost = Heuristic(nb, goal);
                    nb.parent = current;
                    if (!open.Contains(nb)) open.Add(nb);
                }
            }
        }

        // no path
        return new List<GridNode>();
    }

    float Heuristic(GridNode a, GridNode b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // Manhattan for grid
    }

    List<GridNode> ReconstructPath(GridNode goal)
    {
        var path = new List<GridNode>();
        GridNode cur = goal;
        while (cur != null)
        {
            path.Add(cur);
            cur = cur.parent;
        }
        path.Reverse();
        return path;
    }

    IEnumerable<GridNode> GetNeighbors(GridNode n)
    {
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };
        for (int i = 0; i < 4; i++)
        {
            int nx = n.x + dx[i], ny = n.y + dy[i];
            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                yield return grid[nx, ny];
        }
    }
}