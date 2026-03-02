using UnityEngine;

public class SanctumSyncManager : MonoBehaviour
{
    [Header("Boards")]
    public Board3DView boardP1;
    public Board3DView boardP2;

    [Header("Sanctum position")]
    [Tooltip("Если -1, выбираем случайно 0..4")]
    public int sharedSanctumX = -1;

    private bool _propagating;

    private void Awake()
    {
        if (boardP1 == null || boardP2 == null)
        {
            Debug.LogError("[SanctumSync] Assign boardP1 and boardP2 in Inspector.");
            return;
        }

        int x = sharedSanctumX;
        if (x < 0) x = Random.Range(0, BoardModel.Width);
        x = Mathf.Clamp(x, 0, BoardModel.Width - 1);

        sharedSanctumX = x;

        // Конфигурируем обе доски ОДНИМ X
        boardP1.ConfigureSharedSanctum(x, this);
        boardP2.ConfigureSharedSanctum(x, this);

        Debug.Log($"[SanctumSync] Shared Sanctum X = {x}, Y = 4 for both boards.");
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