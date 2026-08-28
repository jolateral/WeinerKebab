using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusic : MonoBehaviour
{
    public AudioClip music;
    [Range(0f, 1f)] public float volume = 0.35f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.volume = volume;
        audioSource.clip = music;
    }

    private void Start()
    {
        if (music != null) audioSource.Play();
    }
}
