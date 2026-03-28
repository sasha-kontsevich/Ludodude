using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ParanoiaBar: MonoBehaviour
{
    
    [SerializeField] private Image fillerImage;
    [SerializeField] private TMP_Text textValue;

    private GameManager gm = null;

    public void Start()
    {
        if(gm == null)
        {
            gm = GameManager.Instance;
            if(gm == null)
            {
                Debug.Log("Null game manager");
                return;
            }
        }
    }

    public void Update()
    {
        if (textValue)
        {
            textValue.text = $"{gm.Paranoia*100f} %";
        }
        if (fillerImage)
        {
            fillerImage.fillAmount = gm.Paranoia;
        }
    }

}
