using UnityEngine;
using UnityEngine.UI;

public class TextController : MonoBehaviour
{
    [SerializeField]
    protected Text healthText;

    public Text GetHealthText()
    {
        return healthText;
    }

    public void SetHealthText(string health)
    {
        healthText.text = health;
    }
}
