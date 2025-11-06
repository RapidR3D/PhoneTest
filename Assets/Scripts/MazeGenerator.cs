using System.Collections.Generic;
using UnityEngine;

// Generates a maze using recursive backtracker on a grid and spawns wall prefabs.
public class MazeGenerator : MonoBehaviour, IAlgorithm
{
    public int width = 21;  // Should be odd for nice walls
    public int height = 21;
    public float cellSize = 1f;
    public GameObject wallPrefab;

    bool[,] visited;
    GameObject wallsHolder;

    public void StartAlgorithm()
    {
        wallsHolder = new GameObject("MazeWalls");
        wallsHolder.transform.parent = transform;
        GenerateMaze();
    }

    public void StopAlgorithm()
    {
        if (wallsHolder != null) Destroy(wallsHolder);
    }

    void GenerateMaze()
    {
        visited = new bool[width, height];
        // Initialize walls everywhere
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (x % 2 == 0 || y % 2 == 0)
                    CreateWallAt(x, y);

        // carve using recursive backtracker starting at (1,1)
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int start = new Vector2Int(1, 1);
        visited[start.x, start.y] = true;
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Peek();
            var neighbors = new List<Vector2Int>();

            var offsets = new Vector2Int[] { new Vector2Int(0, 2), new Vector2Int(0, -2), new Vector2Int(2, 0), new Vector2Int(-2, 0) };
            foreach (var o in offsets)
            {
                var n = current + o;
                if (n.x > 0 && n.x < width && n.y > 0 && n.y < height && !visited[n.x, n.y])
                    neighbors.Add(n);
            }

            if (neighbors.Count > 0)
            {
                var next = neighbors[Random.Range(0, neighbors.Count)];
                // remove wall between current and next:
                Vector2Int wall = current + (next - current) / 2;
                RemoveWallAt(wall.x, wall.y);
                visited[next.x, next.y] = true;
                stack.Push(next);
            }
            else
            {
                stack.Pop();
            }
        }
    }

    void CreateWallAt(int x, int y)
    {
        if (wallPrefab == null) return;
        Vector3 pos = new Vector3(x * cellSize + cellSize * 0.5f, 0, y * cellSize + cellSize * 0.5f) + transform.position;
        var w = Instantiate(wallPrefab, pos, Quaternion.identity, wallsHolder.transform);
        w.transform.localScale = new Vector3(cellSize, 2f, cellSize);
    }

    void RemoveWallAt(int x, int y)
    {
        // find walls near this position and destroy one (cheap but works for demo)
        Vector3 center = new Vector3(x * cellSize + cellSize * 0.5f, 0, y * cellSize + cellSize * 0.5f) + transform.position;
        var colliders = Physics.OverlapSphere(center, cellSize * 0.2f);
        foreach (var c in colliders)
        {
            if (c.gameObject.transform.parent == wallsHolder.transform)
            {
                Destroy(c.gameObject);
            }
        }
    }
}