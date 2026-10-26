using System;
using UnityEngine;

// Plain data describing one playable character's look. Lives in a list on PlayerController
// (element 0 = Glork, element 1 = Flerb). Nothing here affects gameplay - it's all cosmetic.
[Serializable]
public class CharacterDefinition
{
    public string displayName = "Character";

    [Tooltip("The Animator Controller with this character's running animation (e.g. GlorkAnimator / FlerbAnimator).")]
    public RuntimeAnimatorController runningController;

    [Tooltip("A single standing-still frame. Used when this character is waiting in a maze or left standing behind after a swap. The first frame of the run cycle works fine.")]
    public Sprite idleSprite;
}
