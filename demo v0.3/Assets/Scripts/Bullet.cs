using UnityEngine;

// Attach to Bullet prefab. Requires Collider2D (Is Trigger).
//
// This is now a purely visual tracer - PlayerController resolves the actual hit/miss instantly via
// a hitscan the moment the fire button is pressed, so this object's travel time no longer affects
// whether a shot lands. Speed here is just for how punchy the muzzle-to-target flash looks.
public class Bullet : MonoBehaviour
{
    public float speed = 14f;
    public float lifetime = 3f;

    private Vector2 direction = Vector2.up;

    public void Init(Vector2 fireDirection)
    {
        direction = fireDirection.normalized;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Destroy bullet if it hits a wall
        if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }
}