using System;

// Plain data - no MonoBehaviour needed. One per maze grid cell.
[Serializable]
public class MazeCell
{
    public bool wallN = true;
    public bool wallS = true;
    public bool wallE = true;
    public bool wallW = true;

    // "none", "steam", "wire", "fan"
    public ObstacleType obstacle = ObstacleType.None;
    public FanDirection fanDir = FanDirection.North;
    // How many cells long this fan's wind tunnel stretches along the corridor (1 = old
    // single-cell behavior). Only meaningful on the cell the fan obstacle actually lives on.
    public int fanSpanCells = 1;

    public bool visited = false;
    public bool onPath = false; // true if this cell is part of the single extracted corridor
}

public enum ObstacleType { None, Steam, Wire, Fan }
public enum FanDirection { North, South, East, West }