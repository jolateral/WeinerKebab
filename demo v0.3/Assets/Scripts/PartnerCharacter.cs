using UnityEngine;

// Attach to the Partner prefab (a SpriteRenderer + a trigger Collider2D + this script).
//
// The partner is ALWAYS "whichever character the player is not currently playing as". It reads that
// from PlayerController, so you only need ONE partner prefab - it shows Flerb while you're Glork,
// and Glork while you're Flerb.
//
// When the player touches it: the player's look swaps (cosmetic only), the character they WERE
// stays behind standing in this spot, the wide band's exit gate opens, and this object goes away.
public class PartnerCharacter : MonoBehaviour
{
    [Tooltip("The SpriteRenderer that shows the waiting character. Auto-found if empty.")]
    public SpriteRenderer spriteRenderer;

    [Header("Feel")]
    public float bobHeight = 0.08f;
    public float bobSpeed = 3f;
    [Tooltip("Turn to face the player while waiting (assumes your art faces right by default, like Glork).")]
    public bool facePlayer = true;

    [Header("Audio (optional)")]
    public AudioClip swapSound;
    [Range(0f, 1f)] public float swapSoundVolume = 0.8f;

    private PlayerController player;
    private MazeGenerator mazeGen;
    private int questId = int.MinValue;
    private bool collected = false;
    private Vector3 baseLocalPos;
    private float bobOffsetSeed;
    private float currentBob = 0f;

    // Called by MazeGenerator right after instantiation.
    public void Init(MazeGenerator generator, int questIdentifier)
    {
        mazeGen = generator;
        questId = questIdentifier;
    }

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseLocalPos = spriteRenderer.transform.localPosition;
        bobOffsetSeed = Random.Range(0f, 10f);
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.OnCharacterSwapped -= HandlePlayerSwapped;
            player.OnCharacterSwapped += HandlePlayerSwapped;
        }
        RefreshLook();
    }

    void OnDestroy()
    {
        if (player != null) player.OnCharacterSwapped -= HandlePlayerSwapped;
    }

    private void HandlePlayerSwapped(int newIndex)
    {
        RefreshLook();
    }

    // Always display the character the player is NOT playing right now.
    private void RefreshLook()
    {
        if (spriteRenderer == null || player == null) return;
        var other = player.OtherCharacter;
        if (other != null && other.idleSprite != null) spriteRenderer.sprite = other.idleSprite;
    }

    void Update()
    {
        if (spriteRenderer == null || collected) return;

        currentBob = Mathf.Sin((Time.time + bobOffsetSeed) * bobSpeed) * bobHeight;
        spriteRenderer.transform.localPosition = baseLocalPos + new Vector3(0f, currentBob, 0f);

        if (facePlayer && player != null)
        {
            spriteRenderer.flipX = player.transform.position.x < transform.position.x;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        var p = other.GetComponent<PlayerController>();
        if (p == null) return;
        collected = true;

        // Remember who the player WAS before swapping, so they can be left standing here.
        CharacterDefinition leaving = p.CurrentCharacter;
        bool wasFlipped = p.spriteRenderer != null && p.spriteRenderer.flipX;

        // Prefer the character's idle sprite; if you forgot to assign one, fall back to whatever
        // frame the player is showing right now so the left-behind character is never invisible.
        Sprite leavingSprite = (leaving != null && leaving.idleSprite != null)
            ? leaving.idleSprite
            : (p.spriteRenderer != null ? p.spriteRenderer.sprite : null);
        if (leaving != null && leaving.idleSprite == null)
        {
            Debug.LogWarning("Character '" + leaving.displayName + "' has no Idle Sprite assigned in PlayerController > Characters. Using the current animation frame instead.");
        }
        string leavingName = leaving != null ? leaving.displayName : "Character";

        p.SwapCharacter();
        SpawnLeftBehind(p, leavingName, leavingSprite, wasFlipped);

        if (mazeGen != null) mazeGen.ResolvePartnerQuest(questId);
        if (swapSound != null) AudioSource.PlayClipAtPoint(swapSound, transform.position, swapSoundVolume);

        Destroy(gameObject);
    }

    // The left-behind character is pure scenery: a sprite with NO collider and NO scripts. The player
    // runs straight past it, and since the partner (the only thing that triggers a swap) is gone,
    // it can't be tagged again in this maze.
    private void SpawnLeftBehind(PlayerController p, string characterName, Sprite sprite, bool flipX)
    {
        if (sprite == null) return;

        // Stand exactly where the partner was waiting (minus the idle bob).
        Vector3 standPos = spriteRenderer != null
            ? spriteRenderer.transform.position - new Vector3(0f, currentBob, 0f)
            : transform.position;

        var go = new GameObject("LeftBehind_" + characterName);
        go.transform.position = standPos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.flipX = flipX;

        // Draw on the SAME sorting layer/order the partner used - it was clearly visible there, so
        // the left-behind character can't end up hidden underneath the floor tiles.
        if (spriteRenderer != null)
        {
            sr.sortingLayerID = spriteRenderer.sortingLayerID;
            sr.sortingOrder = spriteRenderer.sortingOrder;
        }
        else if (p.spriteRenderer != null)
        {
            sr.sortingLayerID = p.spriteRenderer.sortingLayerID;
            sr.sortingOrder = p.spriteRenderer.sortingOrder;
        }

        // Same on-screen size as the player's character.
        if (p.spriteRenderer != null) go.transform.localScale = p.spriteRenderer.transform.lossyScale;

        // Hand it to the maze so it's cleaned up with its row once the flood has passed.
        if (mazeGen != null)
        {
            go.transform.SetParent(mazeGen.transform, true);
            mazeGen.RegisterSpawned(go.transform.position, go);
        }
    }
}
