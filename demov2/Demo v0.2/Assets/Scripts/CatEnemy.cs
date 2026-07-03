using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class CatEnemy : MonoBehaviour
{
    [Header("Patrol")]
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;

    private Rigidbody2D rb;
    private Vector3 target;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null) body.bodyType = RigidbodyType2D.Kinematic;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        target = (pointB != null) ? pointB.position : transform.position;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.isGameOver) return;
        if (pointA == null || pointB == null) return;

        Vector3 next = Vector3.MoveTowards(rb.position, target, speed * Time.deltaTime);
        rb.MovePosition(next);

        if (Vector3.Distance(rb.position, target) < 0.05f)
            target = (target == pointA.position) ? pointB.position : pointA.position;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
            GameManager.Instance.GameOver();
    }
}