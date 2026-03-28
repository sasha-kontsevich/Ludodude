using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ParceTooltip: MonoBehaviour
{
    [SerializeField] private TMP_Text textValue;

    private TooltipManager tm = null;

    [Range(0f,1f)]
    public float transpSpeed = 0.2f;
    private CanvasGroup canvasGroup;
    public void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if(tm == null)
        {
            tm = TooltipManager.Instance;
            if(tm == null)
            {
                Debug.Log("Null tooltip manager");
                return;
            }
        }
    }

    public void Update()
    {
        if(textValue == null) return;

        if (tm.IsVisible)
        {
            canvasGroup.alpha = 1f;
            textValue.text = tm.CurrentText;
        }
        else
        {
            canvasGroup.alpha -= transpSpeed * Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(canvasGroup.alpha);
        }
    }
}
