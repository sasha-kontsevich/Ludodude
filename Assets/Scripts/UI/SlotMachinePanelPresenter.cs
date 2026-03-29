using System.Text;
using TMPro;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class SlotMachinePanelPresenter : MonoBehaviour
{
    public static SlotMachinePanelPresenter Instance { get; private set; }

    [SerializeField] private GamblingMachineController machineController;
    [SerializeField] private UIDragging betDrag;
    [SerializeField] private GameObject panelRoot;

    [Header("Optional UI labels")]
    [SerializeField] private TMP_Text betLabel;
    [SerializeField] private TMP_Text outcomeLabel;
    [SerializeField] private TMP_Text symbolsLabel;
    [SerializeField] private TMP_Text balanceLabel;
    [SerializeField] private TMP_Text levelLabel;
    [SerializeField] private TMP_Text fakeChanceLabel;

    [SerializeField] private string betFormat = "Ставка: {0:0.##}";
    [SerializeField] private string balanceFormat = "Баланс: {0:0.##}";
    [SerializeField] private string insufficientFundsFormat = "Недостаточно денег: не хватает {0:0.##}";
    [SerializeField] private string fakeChanceFormat = "Шанс (фиктивный): {0:0.##}%";

    [Header("Visual reels (optional)")]
    [SerializeField] private bool preferVisualReels = true;
    [SerializeField] private bool showSingleVisualRow = true;
    [SerializeField] private bool hideSymbolsLabelWhenUsingVisualReels = true;
    [SerializeField] private bool hideUnusedVisualCells = true;
    [SerializeField] private Image[] reelCells;
    [SerializeField] private SlotSymbolSpriteEntry[] symbolSprites;

    [Header("Spin animation")]
    [SerializeField] private float spinAnimationDuration = 1.1f;
    [SerializeField] private float reelStopDelay = 0.15f;
    [SerializeField] private float spinFrameInterval = 0.075f;
    [SerializeField] private string spinningOutcomeText = "КРУТИМ...";

    private readonly StringBuilder _symbolsBuilder = new StringBuilder(64);
    private SpinResult _queuedSpinResult;
    private bool _suppressImmediateUiUpdate;
    private bool _isSpinAnimationRunning;
    private Coroutine _spinRoutine;
    private readonly SlotSymbolId[] _spinSymbols =
    {
        SlotSymbolId.Cherry,
        SlotSymbolId.Lemon,
        SlotSymbolId.Bell,
        SlotSymbolId.Seven,
        SlotSymbolId.Diamond
    };

    public GamblingMachineController CurrentMachine => machineController;
    public bool IsSpinAnimationRunning => _isSpinAnimationRunning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple SlotMachinePanelPresenter instances found. Keeping the first one.", this);
            return;
        }

        Instance = this;

        if (panelRoot == null)
            panelRoot = gameObject;

        RefreshSymbolsLabelVisibility();
        InitializeVisualReels();
    }

    private void OnValidate()
    {
        RefreshSymbolsLabelVisibility();
        InitializeVisualReels();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        if (machineController != null)
            machineController.OnSpinCompleted += OnSpinCompleted;
    }

    private void OnDisable()
    {
        if (machineController != null)
            machineController.OnSpinCompleted -= OnSpinCompleted;
    }

    public void BindMachine(GamblingMachineController machine, bool clearResult = true)
    {
        if (machineController == machine)
            return;

        if (machineController != null)
            machineController.OnSpinCompleted -= OnSpinCompleted;

        machineController = machine;
        RefreshSymbolsLabelVisibility();

        if (machineController != null && isActiveAndEnabled)
            machineController.OnSpinCompleted += OnSpinCompleted;

        if (clearResult)
            ClearResultLabels();
    }

    public void UnbindMachine(bool clearResult = true)
    {
        if (machineController != null)
            machineController.OnSpinCompleted -= OnSpinCompleted;

        machineController = null;
        RefreshSymbolsLabelVisibility();

        if (clearResult)
            ClearResultLabels();
    }

    public bool IsBoundTo(GamblingMachineController machine)
    {
        return machineController == machine;
    }

    public void ShowPanel()
    {
        EnsurePanelRoot();
        if (panelRoot != null)
            panelRoot.SetActive(true);
        InitializeVisualReels();
    }

    public void HidePanel()
    {
        EnsurePanelRoot();
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (machineController == null)
            return;

        float bet = GetCurrentBet();
        if (betLabel != null)
            betLabel.text = string.Format(betFormat, bet);

        if (balanceLabel != null && GameManager.Instance != null)
            balanceLabel.text = string.Format(balanceFormat, GameManager.Instance.CasinoDeposit);

        float fakeChancePercent = GameManager.Instance != null ? GameManager.Instance.FakeWinChancePercent : 0f;
        if (fakeChanceLabel != null)
            fakeChanceLabel.text = string.Format(fakeChanceFormat, fakeChancePercent);

        if (levelLabel != null)
            levelLabel.text = $"Уровень: {machineController.CurrentLevel} | Мин. ставка: {machineController.CurrentMinBet:0.##} | Шанс: {fakeChancePercent:0.##}%";
        else if (fakeChanceLabel == null && outcomeLabel != null && !_isSpinAnimationRunning && string.IsNullOrEmpty(outcomeLabel.text))
            outcomeLabel.text = string.Format(fakeChanceFormat, fakeChancePercent);
    }

    // Hook this to a button onClick in UI.
    public void SpinFromUi()
    {
        TrySpinFromUi();
    }

    public bool TrySpinFromUi()
    {
        if (machineController == null || _isSpinAnimationRunning)
            return false;

        _suppressImmediateUiUpdate = true;
        _queuedSpinResult = null;

        SpinResult result = machineController.TrySpin(machineController.CurrentSpinCost);
        if (result == null)
        {
            _suppressImmediateUiUpdate = false;
            return false;
        }

        if (!result.IsSuccess)
        {
            _suppressImmediateUiUpdate = false;
            ApplyResultToLabels(result);
            return false;
        }

        if (_spinRoutine != null)
            StopCoroutine(_spinRoutine);
        _spinRoutine = StartCoroutine(PlaySpinAnimation(_queuedSpinResult ?? result));
        return true;
    }

    public bool CanSpinFromUi()
    {
        return machineController != null && !_isSpinAnimationRunning;
    }

    public void ShowInsufficientFundsHint()
    {
        if (outcomeLabel == null || machineController == null)
            return;

        GameManager gm = GameManager.Instance;
        float balance = gm != null ? gm.CasinoDeposit : 0f;
        float required = machineController.CurrentSpinCost;
        float shortage = Mathf.Max(0f, required - balance);
        outcomeLabel.text = string.Format(insufficientFundsFormat, shortage);
    }

    public float GetCurrentBet()
    {
        if (machineController == null)
            return 0f;

        return machineController.CurrentSpinCost;
    }

    private void OnSpinCompleted(SpinResult result)
    {
        if (_suppressImmediateUiUpdate)
        {
            _queuedSpinResult = result;
            return;
        }

        ApplyResultToLabels(result);
    }

    private string BuildOutcomeText(SpinResult result)
    {
        if (result == null)
            return "Нет результата";

        if (!result.IsSuccess)
        {
            if (result.FailureReason == SpinFailureReason.InsufficientFunds)
            {
                float shortage = Mathf.Max(0f, result.BetAmount - result.BalanceBefore);
                return string.Format(insufficientFundsFormat, shortage);
            }

            return $"Ошибка прокрута: {FormatFailureReason(result.FailureReason)}";
        }

        return result.IsWin
            ? $"ВЫИГРЫШ +{result.PayoutAmount:0.##}"
            : BuildLoseText(result);
    }

    private string BuildSymbolsText(SpinResult result)
    {
        if (result == null || result.VisibleSymbols == null || result.VisibleSymbols.Length == 0)
            return string.Empty;

        _symbolsBuilder.Clear();
        int reels = machineController != null && machineController.Config != null
            ? machineController.Config.ReelCount
            : 3;

        for (int i = 0; i < result.VisibleSymbols.Length; i++)
        {
            _symbolsBuilder.Append(result.VisibleSymbols[i]);
            bool endOfRow = reels > 0 && ((i + 1) % reels == 0);
            _symbolsBuilder.Append(endOfRow ? '\n' : " | ");
        }

        return _symbolsBuilder.ToString();
    }

    private void ClearResultLabels()
    {
        if (outcomeLabel != null)
            outcomeLabel.text = string.Empty;

        if (symbolsLabel != null && !ShouldHideSymbolsLabel())
            symbolsLabel.text = string.Empty;

        InitializeVisualReels();
    }

    private void EnsurePanelRoot()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
    }

    private IEnumerator PlaySpinAnimation(SpinResult result)
    {
        _isSpinAnimationRunning = true;
        _suppressImmediateUiUpdate = true;

        if (outcomeLabel != null)
            outcomeLabel.text = spinningOutcomeText;

        int reels = machineController != null && machineController.Config != null
            ? Mathf.Max(1, machineController.Config.ReelCount)
            : 3;
        int rows = machineController != null && machineController.Config != null
            ? Mathf.Max(1, machineController.Config.RowCount)
            : 3;
        bool canUseVisualReels = TryGetVisualRowCount(reels, rows, out int visualRows);

        float elapsed = 0f;
        while (elapsed < spinAnimationDuration)
        {
            if (canUseVisualReels)
                ApplyAnimatedVisualReels(result, reels, rows, visualRows, elapsed);
            else if (symbolsLabel != null)
                symbolsLabel.text = BuildAnimatedSymbolsText(result, reels, rows, elapsed);

            //start sound
            AudioManager.Instance.PlaySound(gambleSound);

            yield return new WaitForSeconds(spinFrameInterval);
            elapsed += spinFrameInterval;
        }

        _suppressImmediateUiUpdate = false;
        _queuedSpinResult = null;
        _isSpinAnimationRunning = false;
        _spinRoutine = null;
        ApplyResultToLabels(result);
    }
    [SerializeField] private string gambleSound = "gambleTick";
    private void ApplyResultToLabels(SpinResult result)
    {
        if (outcomeLabel != null)
            outcomeLabel.text = BuildOutcomeText(result);

        int reels = machineController != null && machineController.Config != null
            ? Mathf.Max(1, machineController.Config.ReelCount)
            : 3;
        int rows = machineController != null && machineController.Config != null
            ? Mathf.Max(1, machineController.Config.RowCount)
            : 3;

        if (TryGetVisualRowCount(reels, rows, out int visualRows))
        {
            ApplyFinalVisualReels(result, reels, rows, visualRows);
            return;
        }

        if (symbolsLabel != null)
            symbolsLabel.text = BuildSymbolsText(result);
    }

    private string BuildAnimatedSymbolsText(SpinResult result, int reels, int rows, float elapsed)
    {
        _symbolsBuilder.Clear();
        float totalStopSpan = reels * reelStopDelay;

        for (int row = 0; row < rows; row++)
        {
            for (int reel = 0; reel < reels; reel++)
            {
                float reelStopTime = Mathf.Max(0f, spinAnimationDuration - totalStopSpan + (reel * reelStopDelay));
                bool reelStopped = elapsed >= reelStopTime;
                SlotSymbolId symbol = reelStopped
                    ? GetResultSymbol(result, reels, rows, row, reel)
                    : _spinSymbols[Random.Range(0, _spinSymbols.Length)];

                _symbolsBuilder.Append(FormatSymbol(symbol));
                _symbolsBuilder.Append(reel == reels - 1 ? '\n' : " | ");
            }
        }

        return _symbolsBuilder.ToString();
    }

    private SlotSymbolId GetResultSymbol(SpinResult result, int reels, int rows, int row, int reel)
    {
        if (result == null || result.VisibleSymbols == null || result.VisibleSymbols.Length == 0)
            return _spinSymbols[Random.Range(0, _spinSymbols.Length)];

        int index = row * reels + reel;
        if (index < 0 || index >= result.VisibleSymbols.Length)
            return _spinSymbols[Random.Range(0, _spinSymbols.Length)];

        return result.VisibleSymbols[index];
    }

    private static string FormatSymbol(SlotSymbolId symbol)
    {
        switch (symbol)
        {
            case SlotSymbolId.Cherry: return "CH";
            case SlotSymbolId.Lemon: return "LM";
            case SlotSymbolId.Bell: return "BL";
            case SlotSymbolId.Seven: return "77";
            case SlotSymbolId.Diamond: return "DM";
            default: return "--";
        }
    }

    private static string FormatFailureReason(SpinFailureReason reason)
    {
        switch (reason)
        {
            case SpinFailureReason.InvalidBet:
                return "некорректная ставка";
            case SpinFailureReason.InsufficientFunds:
                return "недостаточно средств";
            case SpinFailureReason.ConfigurationError:
                return "ошибка конфигурации";
            default:
                return "неизвестная ошибка";
        }
    }

    private static string BuildLoseText(SpinResult result)
    {
        if (result != null && result.BetAmount < GameManager.WinUnlockBet)
            return $"ПРОИГРЫШ (ставка ниже {GameManager.WinUnlockBet:0})";

        return "ПРОИГРЫШ";
    }

    private bool TryGetVisualRowCount(int reels, int rows, out int visualRows)
    {
        visualRows = 0;
        if (!preferVisualReels || reelCells == null || reels <= 0 || rows <= 0)
            return false;

        int maxRowsByCells = reelCells.Length / reels;
        if (maxRowsByCells <= 0)
            return false;

        visualRows = Mathf.Min(rows, maxRowsByCells);
        if (showSingleVisualRow)
            visualRows = Mathf.Min(visualRows, 1);

        return visualRows > 0;
    }

    private void ApplyAnimatedVisualReels(SpinResult result, int reels, int totalRows, int visualRows, float elapsed)
    {
        ApplyVisualCellVisibility(reels, visualRows);
        float totalStopSpan = reels * reelStopDelay;

        for (int row = 0; row < visualRows; row++)
        {
            for (int reel = 0; reel < reels; reel++)
            {
                int index = row * reels + reel;
                if (index < 0 || index >= reelCells.Length || reelCells[index] == null)
                    continue;

                float reelStopTime = Mathf.Max(0f, spinAnimationDuration - totalStopSpan + (reel * reelStopDelay));
                bool reelStopped = elapsed >= reelStopTime;
                SlotSymbolId symbol = reelStopped
                    ? GetResultSymbol(result, reels, totalRows, visualRows, row, reel)
                    : _spinSymbols[Random.Range(0, _spinSymbols.Length)];

                reelCells[index].sprite = GetSpriteForSymbol(symbol);
            }
        }
    }

    private void ApplyFinalVisualReels(SpinResult result, int reels, int totalRows, int visualRows)
    {
        ApplyVisualCellVisibility(reels, visualRows);
        for (int row = 0; row < visualRows; row++)
        {
            for (int reel = 0; reel < reels; reel++)
            {
                int index = row * reels + reel;
                if (index < 0 || index >= reelCells.Length || reelCells[index] == null)
                    continue;

                SlotSymbolId symbol = GetResultSymbol(result, reels, totalRows, visualRows, row, reel);
                reelCells[index].sprite = GetSpriteForSymbol(symbol);
            }
        }
    }

    private void ClearVisualReels()
    {
        if (reelCells == null)
            return;

        for (int i = 0; i < reelCells.Length; i++)
        {
            if (reelCells[i] == null)
                continue;

            reelCells[i].sprite = null;
        }
    }

    private Sprite GetSpriteForSymbol(SlotSymbolId symbol)
    {
        if (symbolSprites == null || symbolSprites.Length == 0)
            return null;

        for (int i = 0; i < symbolSprites.Length; i++)
        {
            if (symbolSprites[i].Symbol != symbol)
                continue;

            return symbolSprites[i].Sprite;
        }

        return null;
    }

    private bool ShouldHideSymbolsLabel()
    {
        int reels = machineController != null && machineController.Config != null
            ? Mathf.Max(1, machineController.Config.ReelCount)
            : 3;
        int rows = machineController != null && machineController.Config != null
            ? Mathf.Max(1, machineController.Config.RowCount)
            : 3;

        return hideSymbolsLabelWhenUsingVisualReels && TryGetVisualRowCount(reels, rows, out _);
    }

    private void RefreshSymbolsLabelVisibility()
    {
        if (symbolsLabel == null)
            return;

        symbolsLabel.gameObject.SetActive(!ShouldHideSymbolsLabel());
    }

    private void InitializeVisualReels()
    {
        int reels = machineController != null && machineController.Config != null
            ? Mathf.Max(1, machineController.Config.ReelCount)
            : 3;
        int rows = machineController != null && machineController.Config != null
            ? Mathf.Max(1, machineController.Config.RowCount)
            : 3;

        if (!TryGetVisualRowCount(reels, rows, out int visualRows))
            return;

        ApplyVisualCellVisibility(reels, visualRows);
        int activeCells = Mathf.Min(reelCells.Length, reels * visualRows);
        for (int i = 0; i < activeCells; i++)
        {
            if (reelCells[i] == null || reelCells[i].sprite != null)
                continue;

            SlotSymbolId symbol = _spinSymbols[i % _spinSymbols.Length];
            reelCells[i].sprite = GetSpriteForSymbol(symbol);
        }
    }

    private void ApplyVisualCellVisibility(int reels, int visualRows)
    {
        if (reelCells == null || reels <= 0 || visualRows <= 0)
            return;

        int activeCells = Mathf.Min(reelCells.Length, reels * visualRows);
        for (int i = 0; i < reelCells.Length; i++)
        {
            Image cell = reelCells[i];
            if (cell == null)
                continue;

            bool isActiveCell = i < activeCells;
            if (!isActiveCell && hideUnusedVisualCells)
            {
                cell.sprite = null;
                cell.color = new Color(cell.color.r, cell.color.g, cell.color.b, 0f);
            }
            else
            {
                cell.color = new Color(cell.color.r, cell.color.g, cell.color.b, 1f);
            }
        }
    }

    private SlotSymbolId GetResultSymbol(SpinResult result, int reels, int totalRows, int visualRows, int displayRow, int reel)
    {
        if (result == null || result.VisibleSymbols == null || result.VisibleSymbols.Length == 0)
            return _spinSymbols[Random.Range(0, _spinSymbols.Length)];

        int sourceRow = GetSourceRowByPayline(result, totalRows, visualRows, displayRow);
        int index = sourceRow * reels + reel;

        if (index < 0 || index >= result.VisibleSymbols.Length)
            return _spinSymbols[Random.Range(0, _spinSymbols.Length)];

        return result.VisibleSymbols[index];
    }

    private static int GetSourceRowByPayline(SpinResult result, int totalRows, int visualRows, int displayRow)
    {
        int maxRow = Mathf.Max(0, totalRows - 1);
        int paylineRow = result != null && result.WinningRows != null && result.WinningRows.Length > 0
            ? Mathf.Clamp(result.WinningRows[0], 0, maxRow)
            : totalRows / 2;

        int firstRow = paylineRow - (visualRows / 2);
        int lastPossibleFirstRow = Mathf.Max(0, totalRows - visualRows);
        firstRow = Mathf.Clamp(firstRow, 0, lastPossibleFirstRow);

        int sourceRow = firstRow + displayRow;
        return Mathf.Clamp(sourceRow, 0, maxRow);
    }

    [ContextMenu("Setup Visual Reels (3x3)")]
    private void SetupVisualReels()
    {
#if UNITY_EDITOR
        Transform automat = FindChildRecursive(transform, "Automat");
        if (automat == null)
        {
            Debug.LogWarning("SlotMachinePanelPresenter: Cannot find child 'Automat' for visual reels setup.", this);
            return;
        }

        Undo.RecordObject(this, "Setup Visual Reels");

        RectTransform gridRoot = EnsureReelsGrid(automat);
        GridLayoutGroup grid = gridRoot.GetComponent<GridLayoutGroup>();
        if (grid == null)
            grid = Undo.AddComponent<GridLayoutGroup>(gridRoot.gameObject);

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.spacing = new Vector2(8f, 8f);
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.cellSize = new Vector2(88f, 68f);

        EnsureReelCells(gridRoot);
        CollectReelCellsFromGrid(gridRoot);

        preferVisualReels = true;
        hideSymbolsLabelWhenUsingVisualReels = true;
        RefreshSymbolsLabelVisibility();

        EditorUtility.SetDirty(this);
        EditorUtility.SetDirty(gridRoot);
        Debug.Log("SlotMachinePanelPresenter: visual reels grid has been set up. Assign symbol sprites in Symbol Sprites.", this);
#else
        Debug.LogWarning("SetupVisualReels is available only in Unity Editor.");
#endif
    }

#if UNITY_EDITOR
    private RectTransform EnsureReelsGrid(Transform automat)
    {
        const string reelsGridName = "ReelsGrid";
        Transform existing = automat.Find(reelsGridName);
        RectTransform gridRoot;
        if (existing == null)
        {
            GameObject gridGo = new GameObject(reelsGridName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(gridGo, "Create Reels Grid");
            gridRoot = gridGo.GetComponent<RectTransform>();
            gridRoot.SetParent(automat, false);
        }
        else
        {
            gridRoot = existing as RectTransform;
        }

        if (gridRoot == null)
            gridRoot = automat.gameObject.AddComponent<RectTransform>();

        gridRoot.anchorMin = new Vector2(0.5f, 0.5f);
        gridRoot.anchorMax = new Vector2(0.5f, 0.5f);
        gridRoot.pivot = new Vector2(0.5f, 0.5f);
        gridRoot.anchoredPosition = new Vector2(-212f, 12f);
        gridRoot.sizeDelta = new Vector2(280f, 220f);
        gridRoot.localScale = Vector3.one;
        return gridRoot;
    }

    private void EnsureReelCells(RectTransform gridRoot)
    {
        const int requiredCount = 9;
        for (int i = gridRoot.childCount; i < requiredCount; i++)
        {
            string cellName = $"Cell_{i + 1:00}";
            GameObject cellGo = new GameObject(cellName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(cellGo, "Create Reel Cell");
            RectTransform cellRect = cellGo.GetComponent<RectTransform>();
            cellRect.SetParent(gridRoot, false);
            cellRect.localScale = Vector3.one;

            Image img = cellGo.GetComponent<Image>();
            img.sprite = null;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }
    }

    private void CollectReelCellsFromGrid(RectTransform gridRoot)
    {
        reelCells = new Image[9];
        int count = Mathf.Min(9, gridRoot.childCount);
        for (int i = 0; i < count; i++)
            reelCells[i] = gridRoot.GetChild(i).GetComponent<Image>();
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
#endif
}

[System.Serializable]
public struct SlotSymbolSpriteEntry
{
    public SlotSymbolId Symbol;
    public Sprite Sprite;
}
