using UnityEngine;
[DefaultExecutionOrder(-100)]

public class SanctumSyncManager : MonoBehaviour
{
    [Header("Boards")]
    public Board3DView boardP1;
    public Board3DView boardP2;

    [Header("Sanctum position")]
    [Tooltip("≈сли -1, выбираем случайно 0..4")]
    public int sharedSanctumX = -1;

    private bool _propagating;

    private void Awake()
    {
        if (boardP1 == null || boardP2 == null)
        {
            Debug.LogError("[SanctumSync] boardP1/boardP2 не назначены.");
            return;
        }

        int x;
        if (sharedSanctumX < 0)
            x = UnityEngine.Random.Range(0, BoardModel.Width);
        else
            x = Mathf.Clamp(sharedSanctumX, 0, BoardModel.Width - 1);

        Debug.Log($"[SanctumSync] Shared Sanctum X={x}");

        boardP1.ConfigureSharedSanctum(x, this);
        boardP2.ConfigureSharedSanctum(x, this);
    }

    public void NotifyBoardRevealed(Board3DView source)
    {
        if (_propagating) return;
        _propagating = true;

        if (source == boardP1)
            boardP2.ForceRevealSanctumFromSync();
        else if (source == boardP2)
            boardP1.ForceRevealSanctumFromSync();

        _propagating = false;
    }
}