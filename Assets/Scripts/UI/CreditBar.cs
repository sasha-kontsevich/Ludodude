using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreditBar: MonoBehaviour
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
        float normalised = Mathf.Clamp01((float)gm.CasinoDeposit / gm.MaxDeposit);
        if (textValue)
        {
            textValue.text = $"{gm.CasinoDeposit} / {gm.MaxDeposit}";
        }
        if (fillerImage)
        {
            fillerImage.fillAmount = normalised;
        }
    }

}
