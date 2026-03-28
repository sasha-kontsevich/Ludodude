using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class UIDragging : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    //diaposon from top where we can begin dragging (percentage)
    public float startDiaposon = 0.1f;

    //diaposon from bottom where we can finish dragging (percentage)
    public float finishDiaposon = 0.1f; 

    private RectTransform panel;
    public float value = 0f;
    private bool isDragging = false;

    public void Start()
    {
        panel = GetComponent<RectTransform>();
    }

    float getNormilizedHeightPosition(PointerEventData eventData)
    {
        Vector2 localPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panel,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        float normalized = Mathf.InverseLerp(
            panel.rect.yMax,
            panel.rect.yMin,
            localPoint.y
        );

        return normalized;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if(getNormilizedHeightPosition(eventData) < startDiaposon){
            isDragging = true;
            Debug.Log("You start dragging");
        }
        else{
            isDragging = false;
            Debug.Log("You fail start dragging");
        }
        
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        
        value = getNormilizedHeightPosition(eventData);
        if(value > 1.0f - finishDiaposon)
        {
            Debug.Log("You finish dragging");
        }
        
    }
}