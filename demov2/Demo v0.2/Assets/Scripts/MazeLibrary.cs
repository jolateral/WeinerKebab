using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MazeEntry
{
    public GameObject mazePrefab;
    public Difficulty difficulty;
}

// Create one of these via Assets > Create > Pipe Runner > Maze Library,
// then drag in your maze prefabs and tag each with a difficulty.
// LevelManager reads from this asset to pick the next maze.
[CreateAssetMenu(fileName = "MazeLibrary", menuName = "Pipe Runner/Maze Library")]
public class MazeLibrary : ScriptableObject
{
    public List<MazeEntry> mazes = new List<MazeEntry>();
}
