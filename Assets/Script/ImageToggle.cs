using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImageToggle : MonoBehaviour
{
    public Image buttonImage;
    public Sprite onSprite;
    public Sprite offSprite;

    private bool isOn = false;

    public void Toggle()
    {
        isOn = !isOn;
        buttonImage.sprite = isOn ? onSprite : offSprite;
    }
}
