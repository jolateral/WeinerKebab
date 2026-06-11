using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CatSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject catPrefab;
    public CameraScroller cameraScroller;
    public Image catWarningFlash; // A full-screen orange semi-transparent UI Image
    public AudioSource warningAudio;  // Optional: assign a snarl sound

    [Header("Spawn Settings")]
    public float firstCatDelay = 15f;
    public float minCatInterval = 20f;
    public float maxCatInterval = 40f;
    public float warningDuration = 2f;

    private bool catIsActive = false;

    private void Start()
    {
        if (catWarningFlash != null)
            catWarningFlash.color = new Color(1f, 0.4f, 0f, 0f); // start invisible

        StartCoroutine(SpawnCatRoutine());
    }

    private IEnumerator SpawnCatRoutine()
    {
        yield return new WaitForSeconds(firstCatDelay);

        while (true)
        {
            if (GameManager.Instance.isGameOver) yield break;
            if (!catIsActive)
            {
                yield return StartCoroutine(WarnAndSpawnCat());
            }
            yield return new WaitForSeconds(Random.Range(minCatInterval, maxCatInterval));
        }
    }

    private IEnumerator WarnAndSpawnCat()
    {
        catIsActive = true;

        // Play warning audio
        if (warningAudio != null) warningAudio.Play();

        // Flash warning image on screen
        if (catWarningFlash != null)
        {
            float flashInterval = 0.25f;
            float elapsed = 0f;
            while (elapsed < warningDuration)
            {
                float alpha = (Mathf.Sin(elapsed * Mathf.PI / flashInterval) > 0) ? 0.5f : 0f;
                catWarningFlash.color = new Color(1f, 0.3f, 0f, alpha);
                elapsed += Time.deltaTime;
                yield return null;
            }
            catWarningFlash.color = new Color(1f, 0.3f, 0f, 0f);
        }

        // Spawn cat above screen top
        float spawnX = Random.Range(-4f, 4f);
        float spawnY = cameraScroller.GetTopEdge() + 2f;

        GameObject cat = Instantiate(catPrefab, new Vector3(spawnX, spawnY, 0), Quaternion.identity);

        // Wait until cat is gone
        yield return new WaitUntil(() => cat == null);
        catIsActive = false;
    }
}