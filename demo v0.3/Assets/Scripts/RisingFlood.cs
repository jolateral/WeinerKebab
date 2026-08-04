using UnityEngine;

// Attach to a "Flood" GameObject - a wide sprite/quad positioned at the bottom of the screen,
// e.g. a red gradient sprite. This is the "camera" the player must stay ahead of.
public class RisingFlood : MonoBehaviour
{
    public GameSettings settings;
    public Transform player;
    public MazeGenerator mazeGenerator;

    [HideInInspector] public float floodWorldY;
    private float elapsed = 0f;
    private bool gameOver = false;

    public System.Action OnPlayerCaught;

    void Start()
    {
        floodWorldY = -settings.killMarginBelowCamera;
    }

    void Update()
    {
        if (gameOver) return;

        elapsed += Time.deltaTime;
        float riseSpeed = settings.floodRiseSpeedBase + settings.floodRiseRampPerSec * elapsed;
        floodWorldY += riseSpeed * Time.deltaTime;

        transform.position = new Vector3(transform.position.x, floodWorldY, transform.position.z);

        mazeGenerator.EnsureGeneratedAhead(floodWorldY);

        if (player != null && player.position.y < floodWorldY)
        {
            gameOver = true;
            OnPlayerCaught?.Invoke();
        }
    }

    public float HeightScore => player != null ? Mathf.Max(0f, player.position.y / settings.cellSize) : 0f;
}
