using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class TopDownPlayerController : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [Header("Audio")]
    [SerializeField] private string footstepsSoundKey = "footsteps_loop";
    [SerializeField] private float movementInputDeadzone = 0.1f;
    [SerializeField] private Animator anim;

    private Rigidbody2D _rb;
    private InputActionMap _playerMap;
    private InputAction _moveAction;
    private InputAction _sprintAction;
    private InputAction _interactAction;
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
        _interactAction = _playerMap?.FindAction("Interact");
        _sprintAction = _playerMap.FindAction("Sprint");
    }

    private void OnEnable()
    {
        _playerMap?.Enable();
    }

    private void OnDisable()
    {
        _playerMap?.Disable();
        StopFootstepsLoop();
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
        float deadzone = Mathf.Max(0f, movementInputDeadzone);
        bool isMoving = input.sqrMagnitude >= deadzone * deadzone && speed > 0f;
        UpdateFootstepsLoop(isMoving);
        Vector2 delta = input * (speed * Time.fixedDeltaTime);
        _rb.MovePosition(_rb.position + delta);
        runAnimation(isMoving);
        OnGetACtion();
    }

    private void UpdateFootstepsLoop(bool isMoving)
    {
        var audioManager = AudioManager.Instance;
        if (audioManager == null || string.IsNullOrWhiteSpace(footstepsSoundKey))
            return;

        audioManager.SetLoopSoundPlaying(footstepsSoundKey, isMoving);
    }

    private void runAnimation(bool ismov)
    {
        if (ismov)
        {
            anim.SetBool("run", true);
        }
        else
        {
            anim.SetBool("run", false);
        } 
    }
    private void OnGetACtion()
    {
        _playerMap?.Enable();
        // Interact в проекте с интеракцией Hold: при коротком нажатии есть started, а performed — только после удержания.
        if (_interactAction != null)
            _interactAction.started += grabAnimation;
    }
    private void grabAnimation(InputAction.CallbackContext _)
    {
        anim.SetBool("run", true);
        anim.SetTrigger("grab");
        anim.SetBool("run", false);
    }

    private void StopFootstepsLoop()
    {
        if (string.IsNullOrWhiteSpace(footstepsSoundKey))
            return;

        AudioManager.Instance?.StopSound(footstepsSoundKey);
    }
}
