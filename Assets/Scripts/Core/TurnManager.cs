using System;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager I { get; private set; }

    public enum Phase
    {
        SetupRoll,
        InitialDeal,

        // Первый цикл (без enemy spell)
        Initial_EnemyPlace,     // соперник ставит 1-й тайл в подземелье текущего владельца
        Initial_OwnerTurn3,     // владелец делает 3 действия (тайл+магия+ход)

        // Обычный цикл
        Normal_EnemySpellOpt,   // соперник кидает магию в подземелье владельца ИЛИ пропускает
        Normal_OwnerTurn3,      // владелец делает 3 действия (тайл+магия+ход)

        EndOfRoundSwap,
        RefillHands,
        CheckWinAfterRefill,

        GameOver
    }

    [Serializable]
    public class PlayerCtx
    {
        public int playerId;          // 1 / 2
        public Board3DView board;     // поле игрока
        public CardDeckManager deck;  // колоды игрока
    }

    [Header("Players")]
    public PlayerCtx p1;
    public PlayerCtx p2;

    [Header("Debug")]
    public Phase phase = Phase.SetupRoll;
    public int firstPlayerId = 1;     // у кого жетон "первого" в текущем раунде
    public int currentOwnerId = 1;    // чьё подземелье сейчас является "owner"
    public bool initialRound = true;  // первый цикл партии

    // “3 галочки”
    private struct OwnerActions
    {
        public bool placed;
        public bool spelled;
        public bool moved;
    }
    private OwnerActions _ownerActions;

    // прогресс раунда
    private int _ownersDoneThisRound = 0;

    // ----- CRYSTALS -----
    // Используем ТВОИ скрипты:
    // - TurnCrystal.cs (каждый кристалл)
    // - TurnCrystalsSet.cs (набор из 4 кристаллов)
    [Header("Crystals (optional)")]
    public TurnCrystalsSet crystalsSet;   // желательно
    public TurnCrystal[] crystalsFallback; // если не хочешь использовать set

    private int _activeActorId = -1;

    private void Awake()
    {
        I = this;
    }

    private void Start()
    {
        StartGame();
    }

    private PlayerCtx GetP(int id) => (id == 1) ? p1 : p2;
    private int Other(int id) => (id == 1) ? 2 : 1;

    // Кто сейчас “активный игрок” (кто может кликать карты/кристаллы)
    private int GetActorIdForCurrentPhase()
    {
        switch (phase)
        {
            case Phase.Initial_EnemyPlace:
            case Phase.Normal_EnemySpellOpt:
                return Other(currentOwnerId);

            case Phase.Initial_OwnerTurn3:
            case Phase.Normal_OwnerTurn3:
                return currentOwnerId;

            default:
                return 0;
        }
    }

    // -------------------- START GAME --------------------

    public void StartGame()
    {
        phase = Phase.SetupRoll;

        int r1 = UnityEngine.Random.Range(1, 5);
        int r2 = UnityEngine.Random.Range(1, 5);
        while (r1 == r2)
        {
            r1 = UnityEngine.Random.Range(1, 5);
            r2 = UnityEngine.Random.Range(1, 5);
        }

        firstPlayerId = (r1 > r2) ? 1 : 2;
        initialRound = true;

        Debug.Log($"[TURN] SetupRoll: P1={r1}, P2={r2}. FirstToken=P{firstPlayerId}");

        phase = Phase.InitialDeal;
        DealInitialHands();

        _ownersDoneThisRound = 0;

        // Первый цикл начинается: owner = firstPlayerId, actor = другой игрок
        BeginInitialEnemyPlace(ownerId: firstPlayerId);
    }

    private void DealInitialHands()
    {
        // стартовая раздача
        p1.deck.RefillToFullNow();
        p2.deck.RefillToFullNow();
    }

    // -------------------- PHASE STARTERS --------------------

    private void BeginInitialEnemyPlace(int ownerId)
    {
        currentOwnerId = ownerId;
        phase = Phase.Initial_EnemyPlace;
        ApplyPhaseGates();
    }

    private void BeginInitialOwnerTurn3()
    {
        ResetOwnerActions();
        phase = Phase.Initial_OwnerTurn3;
        ApplyPhaseGates();
    }

    private void BeginNormalEnemySpellOpt(int ownerId)
    {
        currentOwnerId = ownerId;
        phase = Phase.Normal_EnemySpellOpt;
        ApplyPhaseGates();
    }

    private void BeginNormalOwnerTurn3()
    {
        ResetOwnerActions();
        phase = Phase.Normal_OwnerTurn3;
        ApplyPhaseGates();
    }

    private void EndRound()
    {
        phase = Phase.EndOfRoundSwap;

        // жетон "первого" переходит
        firstPlayerId = Other(firstPlayerId);
        Debug.Log($"[TURN] EndRoundSwap. New FirstToken=P{firstPlayerId}");

        phase = Phase.RefillHands;
        p1.deck.RefillToFullNow();
        p2.deck.RefillToFullNow();

        phase = Phase.CheckWinAfterRefill;
        CheckWinAfterRefill();
        if (phase == Phase.GameOver) { ApplyPhaseGates(); return; }

        _ownersDoneThisRound = 0;
        initialRound = false;

        // новый раунд всегда начинается с owner = firstPlayerId
        BeginNormalEnemySpellOpt(ownerId: firstPlayerId);
    }

    private void CheckWinAfterRefill()
    {
        bool p1Win = p1.board != null && p1.board.IsWinnerNow();
        bool p2Win = p2.board != null && p2.board.IsWinnerNow();

        if (p1Win && p2Win)
        {
            Debug.Log("[WIN] DRAW (оба успели до раздачи).");
            phase = Phase.GameOver;
        }
        else if (p1Win)
        {
            Debug.Log("[WIN] Player1 wins.");
            phase = Phase.GameOver;
        }
        else if (p2Win)
        {
            Debug.Log("[WIN] Player2 wins.");
            phase = Phase.GameOver;
        }
    }

    // -------------------- OWNER ACTIONS --------------------

    private void ResetOwnerActions()
    {
        _ownerActions.placed = false;
        _ownerActions.spelled = false;
        _ownerActions.moved = false;
    }

    private void AfterOwnerActionProgress()
    {
        ApplyPhaseGates();

        if (!(_ownerActions.placed && _ownerActions.spelled && _ownerActions.moved))
            return;

        _ownersDoneThisRound++;

        if (initialRound)
        {
            if (_ownersDoneThisRound < 2)
            {
                // второй owner должен пройти Initial_EnemyPlace
                BeginInitialEnemyPlace(ownerId: Other(currentOwnerId));
            }
            else
            {
                EndRound();
            }
        }
        else
        {
            if (_ownersDoneThisRound < 2)
            {
                BeginNormalEnemySpellOpt(ownerId: Other(currentOwnerId));
            }
            else
            {
                EndRound();
            }
        }
    }

    // -------------------- NOTIFY FROM GAME --------------------
    // Эти методы вызываются из DeckManager и Board3DView

    public void NotifyTileDone(int actorPlayerId)
    {
        if (phase == Phase.GameOver) return;

        // Initial: enemy place -> owner turn3
        if (phase == Phase.Initial_EnemyPlace)
        {
            int expectedActor = Other(currentOwnerId);
            if (actorPlayerId != expectedActor) return;

            Debug.Log($"[TURN] Initial EnemyPlace done by P{actorPlayerId} into Owner=P{currentOwnerId}");
            BeginInitialOwnerTurn3();
            return;
        }

        // OwnerTurn3
        if ((phase == Phase.Initial_OwnerTurn3 || phase == Phase.Normal_OwnerTurn3) && actorPlayerId == currentOwnerId)
        {
            if (_ownerActions.placed) return;
            _ownerActions.placed = true;
            Debug.Log($"[TURN] Owner=P{currentOwnerId} placed tile ?");
            AfterOwnerActionProgress();
        }
    }

    public void NotifySpellDone(int actorPlayerId)
    {
        if (phase == Phase.GameOver) return;

        // Normal: enemy spell -> owner turn3
        if (phase == Phase.Normal_EnemySpellOpt)
        {
            int expectedActor = Other(currentOwnerId);
            if (actorPlayerId != expectedActor) return;

            Debug.Log($"[TURN] EnemySpellOpt done by P{actorPlayerId} into Owner=P{currentOwnerId}");
            BeginNormalOwnerTurn3();
            return;
        }

        // OwnerTurn3
        if ((phase == Phase.Initial_OwnerTurn3 || phase == Phase.Normal_OwnerTurn3) && actorPlayerId == currentOwnerId)
        {
            if (_ownerActions.spelled) return;
            _ownerActions.spelled = true;
            Debug.Log($"[TURN] Owner=P{currentOwnerId} cast spell ?");
            AfterOwnerActionProgress();
        }
    }

    public void NotifyMoveDone(int ownerPlayerId)
    {
        if (phase == Phase.GameOver) return;

        if ((phase == Phase.Initial_OwnerTurn3 || phase == Phase.Normal_OwnerTurn3) && ownerPlayerId == currentOwnerId)
        {
            if (_ownerActions.moved) return;
            _ownerActions.moved = true;
            Debug.Log($"[TURN] Owner=P{currentOwnerId} moved mage ?");
            AfterOwnerActionProgress();
        }
    }

    // --- compat чтобы не править везде названия ---
    public void NotifySpellResolved(int actorPlayerId) => NotifySpellDone(actorPlayerId);
    public void NotifyDungeonResolved(int actorPlayerId) => NotifyTileDone(actorPlayerId);

    // -------------------- MOVEMENT GATE --------------------

    public bool CanMoveOnBoard(Board3DView b)
    {
        if (b == null) return false;
        if (phase != Phase.Initial_OwnerTurn3 && phase != Phase.Normal_OwnerTurn3) return false;
        if (GetP(currentOwnerId).board != b) return false;  // важно: сравниваем по ссылке на board
        if (_ownerActions.moved) return false;
        return true;
    }

    // -------------------- CRYSTALS API (for your TurnCrystal.cs) --------------------

    private void ResetCrystalsIfActorChanged()
    {
        int actor = GetActorIdForCurrentPhase();
        if (actor == 0) return;

        if (actor != _activeActorId)
        {
            _activeActorId = actor;

            if (crystalsSet != null)
                crystalsSet.ResetAll();
            else if (crystalsFallback != null)
            {
                foreach (var c in crystalsFallback)
                    if (c != null) c.ResetCrystal();
            }
        }
    }

    public bool Crystal_SkipEnemySpellPhase()
    {
        if (phase == Phase.GameOver) return false;
        if (phase != Phase.Normal_EnemySpellOpt) return false;

        int actor = GetActorIdForCurrentPhase(); // enemy
        int expected = Other(currentOwnerId);
        if (actor != expected) return false;

        Debug.Log($"[TURN] EnemySpell skipped by P{actor}");
        BeginNormalOwnerTurn3();
        return true;
    }

    public bool Crystal_SkipOwnerPlace()
    {
        if (phase == Phase.GameOver) return false;
        if (phase != Phase.Initial_OwnerTurn3 && phase != Phase.Normal_OwnerTurn3) return false;

        int actor = GetActorIdForCurrentPhase(); // owner
        if (actor != currentOwnerId) return false;
        if (_ownerActions.placed) return false;

        _ownerActions.placed = true;
        Debug.Log($"[TURN] Owner=P{currentOwnerId} skip PLACE ?");
        AfterOwnerActionProgress();
        return true;
    }

    public bool Crystal_SkipOwnerSpell()
    {
        if (phase == Phase.GameOver) return false;
        if (phase != Phase.Initial_OwnerTurn3 && phase != Phase.Normal_OwnerTurn3) return false;

        int actor = GetActorIdForCurrentPhase(); // owner
        if (actor != currentOwnerId) return false;
        if (_ownerActions.spelled) return false;

        _ownerActions.spelled = true;
        Debug.Log($"[TURN] Owner=P{currentOwnerId} skip SPELL ?");
        AfterOwnerActionProgress();
        return true;
    }

    public bool Crystal_SkipOwnerMove()
    {
        if (phase == Phase.GameOver) return false;
        if (phase != Phase.Initial_OwnerTurn3 && phase != Phase.Normal_OwnerTurn3) return false;

        int actor = GetActorIdForCurrentPhase(); // owner
        if (actor != currentOwnerId) return false;
        if (_ownerActions.moved) return false;

        _ownerActions.moved = true;
        Debug.Log($"[TURN] Owner=P{currentOwnerId} skip MOVE ?");
        AfterOwnerActionProgress();
        return true;
    }

    // -------------------- INPUT GATES --------------------

    private void ApplyPhaseGates()
    {
        ResetCrystalsIfActorChanged();

        // по умолчанию всё выключено
        if (p1.deck != null) { p1.deck.SetInputEnabled(false, false); p1.deck.SetTargetBoard(p1.board); }
        if (p2.deck != null) { p2.deck.SetInputEnabled(false, false); p2.deck.SetTargetBoard(p2.board); }

        if (phase == Phase.GameOver)
        {
            Debug.Log("[TURN] GAME OVER.");
            return;
        }

        switch (phase)
        {
            case Phase.Initial_EnemyPlace:
                {
                    int actor = Other(currentOwnerId);
                    var a = GetP(actor);
                    var owner = GetP(currentOwnerId);

                    a.deck.SetTargetBoard(owner.board);
                    a.deck.SetInputEnabled(spells: false, dungeon: true);
                    break;
                }

            case Phase.Initial_OwnerTurn3:
            case Phase.Normal_OwnerTurn3:
                {
                    var owner = GetP(currentOwnerId);

                    bool dungeonAllowed = !_ownerActions.placed;
                    bool spellsAllowed = !_ownerActions.spelled;

                    owner.deck.SetTargetBoard(owner.board);
                    owner.deck.SetInputEnabled(spells: spellsAllowed, dungeon: dungeonAllowed);
                    break;
                }

            case Phase.Normal_EnemySpellOpt:
                {
                    int actor = Other(currentOwnerId);
                    var a = GetP(actor);
                    var owner = GetP(currentOwnerId);

                    a.deck.SetTargetBoard(owner.board);
                    a.deck.SetInputEnabled(spells: true, dungeon: false);
                    break;
                }
        }

        Debug.Log($"[TURN] Phase={phase} | FirstToken=P{firstPlayerId} | Owner=P{currentOwnerId} | Actor=P{GetActorIdForCurrentPhase()} | " +
                  $"OwnerActions: place={_ownerActions.placed} spell={_ownerActions.spelled} move={_ownerActions.moved} | initial={initialRound} done={_ownersDoneThisRound}");
    }
}