using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class TopDownPlayerController : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.5f;

    private Rigidbody2D _rb;
    private InputActionMap _playerMap;
    private InputAction _moveAction;
    private InputAction _sprintAction;
    private CharacterStats _stats;

    public Vector2 MoveInput { get; private set; }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _stats = GetComponent<CharacterStats>();
        _rb.gravityScale = 0f;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        if (inputActions == null)
        {
            Debug.LogError("TopDownPlayerController: назначьте Input Action Asset (InputSystem_Actions).", this);
            enabled = false;
            return;
        }

        _playerMap = inputActions.FindActionMap("Player");
        _moveAction = _playerMap.FindAction("Move");
        _sprintAction = _playerMap.FindAction("Sprint");
    }

    private void OnEnable()
    {
        _playerMap?.Enable();
    }

    private void OnDisable()
    {
        _playerMap?.Disable();
    }

    private void FixedUpdate()
    {
        if (_moveAction == null)
            return;

        Vector2 input = _moveAction.ReadValue<Vector2>();
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        float speedMultiplier = _stats != null ? Mathf.Max(0f, _stats.SpeedMultiplier) : 1f;
        float speed = moveSpeed * speedMultiplier;
        if (_sprintAction != null && _sprintAction.IsPressed())
            speed *= sprintMultiplier;

        MoveInput = input;
        Vector2 delta = input * (speed * Time.fixedDeltaTime);
        _rb.MovePosition(_rb.position + delta);
    }
}
