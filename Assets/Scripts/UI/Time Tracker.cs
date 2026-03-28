using UnityEngine;
using TMPro;
public class TimeTracker: MonoBehaviour
{
    
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
            int hours = gm.Time / 3600;
            int minutes = (gm.Time % 3600) / 60;

            textValue.text = $"{hours:D2}:{minutes:D2}";
        }
    }


}
