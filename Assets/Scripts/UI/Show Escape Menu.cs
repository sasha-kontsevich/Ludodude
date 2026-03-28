using UnityEngine;
using UnityEngine.InputSystem;

public class ShowEscape : MonoBehaviour
{

    public GameObject targetObject;
    [SerializeField] public InputActionAsset UIActions;

    private InputAction toggleAction;

    private void Awake()
    {
        InputActionMap ui_map = UIActions.FindActionMap("UI");
        toggleAction = ui_map.FindAction("Pause");
    }

    private void OnEnable()
    {
        toggleAction.Enable();
        toggleAction.performed += OnToggle;
    }

    private void OnDisable()
    {
        toggleAction.performed -= OnToggle;
        toggleAction.Disable();
    }

    private void OnToggle(InputAction.CallbackContext context)
    {
        targetObject.SetActive(!targetObject.activeSelf);
    }
}