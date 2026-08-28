using UnityEngine;

// Attach to the Wire prefab. Requires a Collider2D with "Is Trigger" checked.
[RequireComponent(typeof(AudioSource))]
public class WireHazard : MonoBehaviour
{
    [Tooltip("Sound played the instant the player is stunned by this wire. Import your mp3 into the project (drag it into Assets/Audio or similar) and drag it here.")]
    public AudioClip stunSound;
    [Range(0f, 1f)] public float stunSoundVolume = 0.8f;

    private bool hasTriggeredThisContact = false;
    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null && !hasTriggeredThisContact)
        {
            player.ApplyStun();
            if (stunSound != null) audioSource.PlayOneShot(stunSound, stunSoundVolume);
            hasTriggeredThisContact = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            hasTriggeredThisContact = false; // allow re-trigger on a fresh contact later
        }
    }
}