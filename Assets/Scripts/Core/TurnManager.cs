using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager I { get; private set; }

    public enum Phase
    {
        SetupRoll,          // бросок d4, выдача жетона
        InitialDeal,        // раздать по 2 магии и 2 тайла каждому
        P1_EnemyPlace,      // (первый цикл) соперник ставит 1й тайл в подземелье игрока
        P1_OwnerPlace,      // владелец ставит 2й тайл себе
        P1_OwnerSpell,      // владелец магию себе
        P1_Move,            // владелец двигает мага
        P2_EnemyPlace,      // (первый цикл) соперник ставит 1й тайл во 2е подземелье
        P2_OwnerPlace,
        P2_OwnerSpell,
        P2_Move,

        // последующие циклы:
        P1_EnemySpellOpt,   // соперник магию в подземелье игрока 1 или пас
        P1_OwnerPlace1,
        P1_OwnerSpell2,
        P1_Move2,

        P2_EnemySpellOpt,
        P2_OwnerPlace1,
        P2_OwnerSpell2,
        P2_Move2,

        EndOfRoundSwap,     // передать жетон, сменить порядок
        RefillHands,        // добор недостающих до 2+2
        CheckWinAfterRefill // проверка победы/ничьей (после добора!)
    }

    [System.Serializable]
    public class PlayerCtx
    {
        public int playerId;                 // 1 / 2 (физический игрок)
        public Board3DView board;            // его поле
        public CardDeckManager deck;         // его колоды
        public bool hasFirstToken;           // “жетон первого хода”
    }

    [Header("Players (физические)")]
    public PlayerCtx p1;
    public PlayerCtx p2;

    [Header("Debug")]
    public Phase phase = Phase.SetupRoll;

    // кто “первый игрок” в текущем цикле (это НЕ поле, а порядок)
    private int firstPlayerId; // 1 или 2
    private bool initialRound = true;

    private void Awake()
    {
        I = this;
    }

    private void Start()
    {
        StartGame();
    }

    public void StartGame()
    {
        phase = Phase.SetupRoll;

        // d4 бросок (чисто кто первый)
        int roll1 = UnityEngine.Random.Range(1, 5);
        int roll2 = UnityEngine.Random.Range(1, 5);

        // если равенство — перебрасываем (чтобы не спорить)
        while (roll1 == roll2)
        {
            roll1 = UnityEngine.Random.Range(1, 5);
            roll2 = UnityEngine.Random.Range(1, 5);
        }

        firstPlayerId = roll1 > roll2 ? 1 : 2;

        p1.hasFirstToken = (firstPlayerId == 1);
        p2.hasFirstToken = (firstPlayerId == 2);

        // 1) initial deal
        phase = Phase.InitialDeal;
        DealInitialHands();

        // 2) запускаем первый цикл
        GoToFirstTurnStart();
    }

    private void DealInitialHands()
    {
        // добираем ДО 2 в каждой колоде (магия и тайлы)
        p1.deck.RefillToFullNow();
        p2.deck.RefillToFullNow();
    }

    private void GoToFirstTurnStart()
    {
        initialRound = true;

        // В первом цикле: игрок, который НЕ первый, ставит 1й тайл в подземелье первого.
        if (firstPlayerId == 1)
            phase = Phase.P1_EnemyPlace;
        else
            phase = Phase.P2_EnemyPlace;

        ApplyPhaseGates();
    }

    /// <summary>
    /// Вызывай из UI кнопкой "Next" / "Pass" / или автоматически после успешного действия.
    /// </summary>
    public void NextPhase()
    {
        // Переходы по твоему регламенту
        switch (phase)
        {
            // --- Первый цикл ---
            case Phase.P1_EnemyPlace: phase = Phase.P1_OwnerPlace; break;
            case Phase.P1_OwnerPlace: phase = Phase.P1_OwnerSpell; break;
            case Phase.P1_OwnerSpell: phase = Phase.P1_Move; break;
            case Phase.P1_Move: phase = Phase.P2_EnemyPlace; break;

            case Phase.P2_EnemyPlace: phase = Phase.P2_OwnerPlace; break;
            case Phase.P2_OwnerPlace: phase = Phase.P2_OwnerSpell; break;
            case Phase.P2_OwnerSpell: phase = Phase.P2_Move; break;
            case Phase.P2_Move:
                // передаём жетон и уходим в обычный цикл
                phase = Phase.EndOfRoundSwap;
                break;

            // --- Обычный цикл ---
            case Phase.P1_EnemySpellOpt: phase = Phase.P1_OwnerPlace1; break;
            case Phase.P1_OwnerPlace1: phase = Phase.P1_OwnerSpell2; break;
            case Phase.P1_OwnerSpell2: phase = Phase.P1_Move2; break;
            case Phase.P1_Move2: phase = Phase.P2_EnemySpellOpt; break;

            case Phase.P2_EnemySpellOpt: phase = Phase.P2_OwnerPlace1; break;
            case Phase.P2_OwnerPlace1: phase = Phase.P2_OwnerSpell2; break;
            case Phase.P2_OwnerSpell2: phase = Phase.P2_Move2; break;
            case Phase.P2_Move2:
                phase = Phase.EndOfRoundSwap;
                break;

            case Phase.EndOfRoundSwap:
                SwapFirstToken();
                phase = Phase.RefillHands;
                RefillHands();
                phase = Phase.CheckWinAfterRefill;
                CheckWinAfterRefill();
                // новый цикл начинается с того, кто теперь “первый”
                phase = (firstPlayerId == 1) ? Phase.P1_EnemySpellOpt : Phase.P2_EnemySpellOpt;
                initialRound = false;
                break;
        }

        ApplyPhaseGates();
    }

    private void SwapFirstToken()
    {
        // жетон переходит
        firstPlayerId = (firstPlayerId == 1) ? 2 : 1;
        p1.hasFirstToken = (firstPlayerId == 1);
        p2.hasFirstToken = (firstPlayerId == 2);
    }

    private void RefillHands()
    {
        // “тянут недостающие карты”
        p1.deck.RefillToFullNow();
        p2.deck.RefillToFullNow();
    }

    private void CheckWinAfterRefill()
    {
        bool p1Win = p1.board != null && p1.board.IsWinnerNow(); // сделаем метод ниже
        bool p2Win = p2.board != null && p2.board.IsWinnerNow();

        if (p1Win && p2Win)
        {
            Debug.Log("[WIN] DRAW (оба успели до раздачи).");
            // TODO: UI “ничья”
        }
        else if (p1Win)
        {
            Debug.Log("[WIN] Player1 wins.");
            // TODO: UI “П1 победа”
        }
        else if (p2Win)
        {
            Debug.Log("[WIN] Player2 wins.");
            // TODO: UI “П2 победа”
        }
    }

    /// <summary>
    /// Вот тут мы включаем/выключаем доступ к колодам и указываем “куда целиться”.
    /// </summary>
    private void ApplyPhaseGates()
    {
        // по умолчанию всё выключено
        p1.deck.SetInputEnabled(false, false);
        p2.deck.SetInputEnabled(false, false);

        // и по умолчанию “таргет” — своё поле
        p1.deck.SetTargetBoard(p1.board);
        p2.deck.SetTargetBoard(p2.board);

        // В зависимости от фазы включаем только нужное
        switch (phase)
        {
            // --- Первый цикл ---
            // EnemyPlace: активен соперник, но таргет = поле владельца
            case Phase.P1_EnemyPlace:
                // игрок2 ставит тайл в поле игрок1
                p2.deck.SetTargetBoard(p1.board);
                p2.deck.SetInputEnabled(spells: false, dungeon: true);
                break;

            case Phase.P1_OwnerPlace:
                p1.deck.SetTargetBoard(p1.board);
                p1.deck.SetInputEnabled(spells: false, dungeon: true);
                break;

            case Phase.P1_OwnerSpell:
                p1.deck.SetTargetBoard(p1.board);
                p1.deck.SetInputEnabled(spells: true, dungeon: false);
                break;

            case Phase.P1_Move:
                // ход мага — колоды неактивны
                break;

            case Phase.P2_EnemyPlace:
                // игрок1 ставит тайл в поле игрок2
                p1.deck.SetTargetBoard(p2.board);
                p1.deck.SetInputEnabled(spells: false, dungeon: true);
                break;

            case Phase.P2_OwnerPlace:
                p2.deck.SetTargetBoard(p2.board);
                p2.deck.SetInputEnabled(spells: false, dungeon: true);
                break;

            case Phase.P2_OwnerSpell:
                p2.deck.SetTargetBoard(p2.board);
                p2.deck.SetInputEnabled(spells: true, dungeon: false);
                break;

            case Phase.P2_Move:
                break;

            // --- Обычный цикл ---
            case Phase.P1_EnemySpellOpt:
                // соперник2 может кастануть в подземелье игрока1
                p2.deck.SetTargetBoard(p1.board);
                p2.deck.SetInputEnabled(spells: true, dungeon: false);
                break;

            case Phase.P1_OwnerPlace1:
                p1.deck.SetTargetBoard(p1.board);
                p1.deck.SetInputEnabled(spells: false, dungeon: true);
                break;

            case Phase.P1_OwnerSpell2:
                p1.deck.SetTargetBoard(p1.board);
                p1.deck.SetInputEnabled(spells: true, dungeon: false);
                break;

            case Phase.P1_Move2:
                break;

            case Phase.P2_EnemySpellOpt:
                p1.deck.SetTargetBoard(p2.board);
                p1.deck.SetInputEnabled(spells: true, dungeon: false);
                break;

            case Phase.P2_OwnerPlace1:
                p2.deck.SetTargetBoard(p2.board);
                p2.deck.SetInputEnabled(spells: false, dungeon: true);
                break;

            case Phase.P2_OwnerSpell2:
                p2.deck.SetTargetBoard(p2.board);
                p2.deck.SetInputEnabled(spells: true, dungeon: false);
                break;

            case Phase.P2_Move2:
                break;
        }

        Debug.Log($"[TURN] Phase = {phase}. FirstToken = P{firstPlayerId}");
    }

    // Утилита: можно ли сейчас игроку трогать тип карт
    public bool CanUseDungeon(int playerId) =>
        (playerId == 1 ? p1.deck : p2.deck).IsDungeonEnabled();

    public bool CanUseSpells(int playerId) =>
        (playerId == 1 ? p1.deck : p2.deck).IsSpellsEnabled();
}