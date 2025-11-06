using System.Collections.Generic;
using UnityEngine;

// Simple component to move an agent along a list of GridNode world positions
public class PathFollower : MonoBehaviour
{
    List<GridNode> path;
    int idx = 0;
    float speed = 3f;
    float cellSize;
    Vector3 origin;

    public void Initialize(List<GridNode> path, float cellSize, Vector3 origin)
    {
        this.path = new List<GridNode>(path);
        this.cellSize = cellSize;
        this.origin = origin;
        idx = 0;
    }

    void Update()
    {
        if (path == null || path.Count == 0) return;
        Vector3 target = path[Mathf.Min(idx, path.Count - 1)].WorldPosition(cellSize, origin);
        Vector3 dir = (target - transform.position);
        if (dir.magnitude < 0.05f)
        {
            idx++;
            if (idx >= path.Count) return;
            target = path[idx].WorldPosition(cellSize, origin);
            dir = (target - transform.position);
        }
        transform.position += dir.normalized * speed * Time.deltaTime;
        if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir.normalized);
    }
}