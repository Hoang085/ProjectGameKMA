using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BGController : SingletonG1<BGController>
{
    public Sprite[] backgrounds;

    public SpriteRenderer bgImage;

    public override void Awake()
    {
        MakeSingleton(false);
    }

    public void Start()
    {
        ChangeSprite();
    }

    public void ChangeSprite()
    {
        if (bgImage != null && backgrounds != null && backgrounds.Length > 0)
        {
            int randomIdx = Random.Range(0, backgrounds.Length);

            if (backgrounds[randomIdx] != null)
            {
                bgImage.sprite = backgrounds[randomIdx];
            }
        }
    }

}
