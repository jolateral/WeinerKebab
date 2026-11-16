using UnityEngine;

// Attach to any GameObject in the scene (e.g. your Canvas). Drag the Fire Button's RectTransform
// into "Fire Button". Pins the button to a bottom corner so it sits under the thumb, and lets the
// player flip left/right-handed (remembered between runs).
public class FireButtonPlacer : MonoBehaviour
{
    public RectTransform fireButton;
    [Tooltip("Distance from the screen corner, in canvas units. Larger Y lifts it up out of the swipe-down/phone-edge zone.")]
    public Vector2 margin = new Vector2(90f, 200f);
    public Vector2 buttonSize = new Vector2(220f, 220f);
    public bool rightHandedByDefault = true;

    private const string Key = "VentCrawler_RightHanded";
    private bool rightHanded;

    void Awake()
    {
        rightHanded = PlayerPrefs.GetInt(Key, rightHandedByDefault ? 1 : 0) == 1;
        Apply();
    }

    // Hook this to a small "swap sides" UI button's OnClick.
    public void ToggleHand()
    {
        rightHanded = !rightHanded;
        PlayerPrefs.SetInt(Key, rightHanded ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
    }

    private void Apply()
    {
        if (fireButton == null) return;
        float x = rightHanded ? 1f : 0f;
        fireButton.anchorMin = new Vector2(x, 0f);
        fireButton.anchorMax = new Vector2(x, 0f);
        fireButton.pivot = new Vector2(x, 0f);
        fireButton.sizeDelta = buttonSize;
        fireButton.anchoredPosition = new Vector2(rightHanded ? -margin.x : margin.x, margin.y);
    }
}