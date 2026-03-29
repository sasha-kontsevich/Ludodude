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
        if (gm == null)
            return;

        float chance01 = gm.FakeWinChance01;
        float chancePercent = gm.FakeWinChancePercent;

        if (textValue)
        {
            textValue.text = $"{chancePercent:0.##}%";
        }
        if (fillerImage)
        {
            fillerImage.fillAmount = chance01;
        }
    }

}
