using UnityEngine;
using UnityEngine.InputSystem;

public class ShowEscape : MonoBehaviour
{

    public GameObject targetObject;
    [SerializeField] public InputActionAsset UIActions;

    private InputAction toggleAction;

    private GameManager gm = null;
    private void Start()
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
        if (targetObject.activeSelf)gm.Resume();
        else gm.Pause();
        targetObject.SetActive(!targetObject.activeSelf);
    }
}