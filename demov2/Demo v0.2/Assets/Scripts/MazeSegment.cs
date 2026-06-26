using UnityEngine;

// Put this on the root object of every maze prefab. entryPoint marks
// where this maze connects to the previous one; exitPoint marks where
// the next maze will connect (and should have a MazeExitMarker on it,
// or on a child at the same position).
public class MazeSegment : MonoBehaviour
{
    public Transform entryPoint;
    public Transform exitPoint;
}
