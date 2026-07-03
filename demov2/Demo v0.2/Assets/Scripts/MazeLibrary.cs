using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MazeEntry
{
    public GameObject mazePrefab;
    public Difficulty difficulty;
}

[CreateAssetMenu(fileName = "MazeLibrary", menuName = "Pipe Runner/Maze Library")]
public class MazeLibrary : ScriptableObject
{
    public List<MazeEntry> mazes = new List<MazeEntry>();
}