using UnityEngine;

public class GridNode
{
    public int x, y;
    public bool walkable = true;
    public GridNode parent;
    public float gCost, hCost;
    public float fCost => gCost + hCost;

    public GridNode(int x, int y, bool walkable = true)
    {
        this.x = x; this.y = y; this.walkable = walkable;
    }

    public Vector3 WorldPosition(float cellSize, Vector3 origin)
    {
        return origin + new Vector3(x * cellSize + cellSize * 0.5f, 0, y * cellSize + cellSize * 0.5f);
    }
}