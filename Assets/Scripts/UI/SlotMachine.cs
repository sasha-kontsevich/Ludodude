using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
public class PanelSpriteDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private Image targetImage;
    private RectTransform panel;
    public Sprite[] sprites;

    [Range(0f, 1f)]
    public float threshold = 0.2f;
    private float percent = 0f; 
    private bool reachedBottom = false;
    private bool dragging = false;

    void Start()
    {   
        targetImage = GetComponent<Image>();
        panel = GetComponent<RectTransform>();
        if (sprites.Length > 0)
            targetImage.sprite = sprites[0];
            
    }

    private float GetPercent(PointerEventData eventData)
    {
        Vector2 localPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panel,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        float height = panel.rect.height;
        float y = (height / 2 - localPoint.y) / height;

        return Mathf.Clamp01(y);
    }
    private void UpdateSprite(float percent)
    {
        if (sprites == null || sprites.Length == 0) return;

        int index = Mathf.FloorToInt(percent * sprites.Length);

        // чтобы не выйти за массив
        index = Mathf.Clamp(index, 0, sprites.Length - 1);

        targetImage.sprite = sprites[index];
    }


    public void OnPointerDown(PointerEventData eventData)
    {
        if(reachedBottom) return;

        float cur_percent = GetPercent(eventData);

        if (Mathf.Abs( percent-cur_percent) < threshold)
        {
            Debug.Log("Клик рядом с рукой 10%");
            dragging = true;
        }
        else
        {
            dragging = false;
        }

        UpdateSprite(percent);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if(!dragging || reachedBottom) return;
        percent = GetPercent(eventData);
        UpdateSprite(percent);

        if (!reachedBottom && percent >= 1f-threshold)
        {
            Debug.Log("Достигли последних 10%");
            reachedBottom = true;
            dragging = false;

            returnHand();
        }
    }

    [SerializeField] private float frameDelay = 0.05f;

    private Coroutine returnCoroutine;

    private void returnHand()
    {
        if (returnCoroutine != null)
            StopCoroutine(returnCoroutine);

        returnCoroutine = StartCoroutine(ReturnAnimation());
    }

    private IEnumerator ReturnAnimation()
    {
        for (; percent >= 0; percent -= 0.1f)
        {
            UpdateSprite(percent);
            yield return new WaitForSeconds(frameDelay);
        }
        if(percent < 0) percent = 0f;
    }
    

}