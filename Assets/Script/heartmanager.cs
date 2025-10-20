using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class heartmanager : MonoBehaviour
{
    public int health = 3;
    public Image[] healthbar;
    public Sprite fullheart;
    public Sprite emptyheart;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        foreach (Image img in healthbar)
        {
            img.sprite = emptyheart;
        }
        for (int i = 0; i < health; i++)
        {
            healthbar[i].sprite = fullheart;
        }
    }

    public void LoseHeart()
    {
        if (health > 0)
        {
            health--;
            Debug.Log($"Mất 1 heart! Còn lại: {health}");
        }
        else
        {
            Debug.Log("Hết heart! Game Over!");
        }
    }
}
