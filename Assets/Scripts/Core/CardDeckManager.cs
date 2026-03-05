using System;
using System.Collections.Generic;
using UnityEngine;

/// Менеджер двух колод:
/// - колода заклинаний (Spell cards)
/// - колода карт подземелья (Dungeon / Path cards)
///
/// Колоды собираются по настройкам в инспекторе:
/// для каждого типа карты указываешь префаб и количество.
/// В начале игры раздаётся по 2 карты магии и 2 карты подземелья
/// на указанные слоты. Новая пара выдаётся ТОЛЬКО когда обе
/// карты соответствующего типа были использованы.
/// Когда колода закончилась – новые карты больше не выдаются.
///
/// Дополнительно:
/// - есть отдельный сброс для магии и отдельный сброс для тайлов.
///   Туда попадают все использованные карты и карты, сброшенные
///   игроком в зону сброса.
/// </summary>
public class CardDeckManager : MonoBehaviour
{
 
    [Header("Связь с доской (необязательно, просто для удобства)")]
    public Board3DView board;
    public int ownerPlayerId = 1;     // 1 или 2
    public Camera mainCamera;         // (опционально) если хочешь задавать камеру
    // ---------- НАСТРОЙКА КОЛОД В ИНСПЕКТОРЕ ----------

    [Serializable]
    public class SpellCardEntry
    {
        [Tooltip("Префаб карты заклинания (на руках). На нём висит DraggableSpellCard.")]
        public DraggableSpellCard cardPrefab;

        [Min(0)]
        [Tooltip("Сколько таких карт положить в колоду.")]
        public int count = 1;
    }

    [Serializable]
    public class DungeonCardEntry
    {
        [Tooltip("Префаб карты тайла подземелья (на руках). На нём висит DraggableCardTile.")]
        public GameObject cardPrefab;

        [Min(0)]
        [Tooltip("Сколько таких карт положить в колоду.")]
        public int count = 1;
    }

    [Header("Колода заклинаний")]
    [Tooltip("Какие карты магии есть в колоде и по сколько штук каждой.")]
    public SpellCardEntry[] spellCardConfigs;

    [Tooltip("Слоты (объекты в сцене), куда кладутся 2 карты магии.")]
    public Transform[] spellSlots = new Transform[2];

    [Header("Колода подземелья (тайлы)")]
    [Tooltip("Какие карты тайлов есть в колоде и по сколько штук каждой.")]
    public DungeonCardEntry[] dungeonCardConfigs;

    [Tooltip("Слоты (объекты в сцене), куда кладутся 2 карты подземелья.")]
    public Transform[] dungeonSlots = new Transform[2];

    // ---------- ДЕЙСТВУЮЩИЕ КОЛОДЫ ----------

    // реальные колоды (список «карт») после разворачивания по count
    private readonly List<SpellCardEntry> _spellDeck = new List<SpellCardEntry>();
    private readonly List<DungeonCardEntry> _dungeonDeck = new List<DungeonCardEntry>();

    // какие конкретные объекты карт сейчас лежат на столе в слотах
    private GameObject[] _activeSpellCards;
    private GameObject[] _activeDungeonCards;

    // ---------- ОТДЕЛЬНЫЕ СБРОСЫ ----------

    [Header("Discard piles (для дебага)")]
    [SerializeField]
    private List<SpellType> _spellDiscard = new List<SpellType>();

    [SerializeField]
    private List<TileDefinition> _dungeonDiscard = new List<TileDefinition>();

    [Header("Turn control")]
    [SerializeField] private bool spellsEnabled = true;
    [SerializeField] private bool dungeonEnabled = true;

    private Board3DView targetBoardOverride = null;

    public bool IsSpellsEnabled() => spellsEnabled;
    public bool IsDungeonEnabled() => dungeonEnabled;

    public void SetInputEnabled(bool spells, bool dungeon)
    {
        spellsEnabled = spells;
        dungeonEnabled = dungeon;
    }

    public void SetTargetBoard(Board3DView target)
    {
        targetBoardOverride = target;
    }

    public Board3DView GetTargetBoard()
    {
        return targetBoardOverride != null ? targetBoardOverride : board;
    }
    private void Awake()
    {
        // Лучше настроить board в инспекторе для каждого игрока.
        if (board == null)
            board = FindFirstObjectByType<Board3DView>();

        BuildSpellDeck();
        BuildDungeonDeck();

        _activeSpellCards = new GameObject[spellSlots.Length];
        _activeDungeonCards = new GameObject[dungeonSlots.Length];
    }

    private void Start()
    {
        // в начале партии раздаём по 2 карты каждого типа
        RefillSpellSlotsIfAllEmpty(force: true);
        RefillDungeonSlotsIfAllEmpty(force: true);
    }

    // ---------- СБОРКА И ПЕРЕТАСОВКА КОЛОД ----------

    private void BuildSpellDeck()
    {
        _spellDeck.Clear();

        if (spellCardConfigs == null)
            return;

        foreach (var entry in spellCardConfigs)
        {
            if (entry == null || entry.cardPrefab == null || entry.count <= 0)
                continue;

            for (int i = 0; i < entry.count; i++)
            {
                _spellDeck.Add(entry);
            }
        }

        Shuffle(_spellDeck);
    }

    private void BuildDungeonDeck()
    {
        _dungeonDeck.Clear();

        if (dungeonCardConfigs == null)
            return;

        foreach (var entry in dungeonCardConfigs)
        {
            if (entry == null || entry.cardPrefab == null || entry.count <= 0)
                continue;

            for (int i = 0; i < entry.count; i++)
            {
                _dungeonDeck.Add(entry);
            }
        }

        Shuffle(_dungeonDeck);
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    // ---------- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ КАРТ ----------

    /// <summary>
    /// Вызывается картой заклинания, когда заклинание УСПЕШНО применено
    /// или карта сброшена в зону сброса.
    /// </summary>
    public void OnSpellCardUsed(DraggableSpellCard card)
    {
        if (card == null) return;

        // Кладём в отдельный сброс колоды магии
        _spellDiscard.Add(card.spellType);

        HandleSpellCardUsed(card.gameObject);
    }

    /// <summary>
    /// Вызывается картой подземелья, когда тайл УСПЕШНО поставлен
    /// или карта сброшена в зону сброса.
    /// </summary>
    public void OnDungeonCardUsed(GameObject cardGO)
    {
        if (cardGO == null) return;

        // Пытаемся вытащить информацию о тайле для сброса
        var tileCard = cardGO.GetComponent<DraggableCardTile>();
        if (tileCard != null && tileCard.tileDef != null)
        {
            _dungeonDiscard.Add(tileCard.tileDef);
        }

        HandleDungeonCardUsed(cardGO);
    }

    // ---------- ВНУТРЕННЯЯ ЛОГИКА ДЛЯ ЗАКЛИНАНИЙ ----------

    private void HandleSpellCardUsed(GameObject cardGO)
    {
        if (_activeSpellCards == null)
            return;

        int index = -1;
        for (int i = 0; i < _activeSpellCards.Length; i++)
        {
            if (_activeSpellCards[i] == cardGO)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            // карта не найдена среди слотов — возможно, не из колоды
            Destroy(cardGO);
            return;
        }

        // удаляем карту из слота
        Destroy(cardGO);
        _activeSpellCards[index] = null;

        // проверяем, остались ли ещё карты в слотах
        bool allEmpty = true;
        for (int i = 0; i < _activeSpellCards.Length; i++)
        {
            if (_activeSpellCards[i] != null)
            {
                allEmpty = false;
                break;
            }
        }

        // НОВЫЕ карты выдаём только когда все слоты магии пусты
        if (allEmpty)
        {
            RefillSpellSlotsIfAllEmpty(force: false);
        }
    }

    private void RefillSpellSlotsIfAllEmpty(bool force)
    {
        if (spellSlots == null || spellSlots.Length == 0)
            return;

        if (_spellDeck.Count == 0)
        {
            Debug.Log($"[Deck P{ownerPlayerId}] Spell deck empty.");
            return;
        }

        if (!force)
        {
            for (int i = 0; i < _activeSpellCards.Length; i++)
                if (_activeSpellCards[i] != null)
                    return;
        }

        for (int i = 0; i < spellSlots.Length; i++)
        {
            if (_spellDeck.Count == 0)
                break;

            var slot = spellSlots[i];
            if (slot == null) continue;
            if (_activeSpellCards[i] != null) continue;

            var entry = _spellDeck[_spellDeck.Count - 1];
            _spellDeck.RemoveAt(_spellDeck.Count - 1);

            // Инстанс БЕЗ родителя (важно для твоего масштаба)
            DraggableSpellCard card = Instantiate(entry.cardPrefab, slot.position, slot.rotation);

            // КАК ТЫ ХОЧЕШЬ: сохраняем world scale/size
            card.transform.SetParent(slot, true); // НЕ УДАЛЯТЬ

            // ЖЁСТКО инжектим “свою” доску и “свою” колоду
            card.board = board;                          // BoardRoot_PlayerX
            card.deckManager = this;                     // DeckManager_PlayerX
            card.mainCamera = mainCamera != null ? mainCamera : Camera.main;

            _activeSpellCards[i] = card.gameObject;
        }
    }

    private void RefillDungeonSlotsIfAllEmpty(bool force)
    {
        if (dungeonSlots == null || dungeonSlots.Length == 0)
            return;

        if (_dungeonDeck.Count == 0)
        {
            Debug.Log($"[Deck P{ownerPlayerId}] Dungeon deck empty.");
            return;
        }

        if (!force)
        {
            for (int i = 0; i < _activeDungeonCards.Length; i++)
                if (_activeDungeonCards[i] != null)
                    return;
        }

        for (int i = 0; i < dungeonSlots.Length; i++)
        {
            if (_dungeonDeck.Count == 0)
                break;

            var slot = dungeonSlots[i];
            if (slot == null) continue;
            if (_activeDungeonCards[i] != null) continue;

            var entry = _dungeonDeck[_dungeonDeck.Count - 1];
            _dungeonDeck.RemoveAt(_dungeonDeck.Count - 1);

            GameObject cardGO = Instantiate(entry.cardPrefab, slot.position, slot.rotation);

            // КАК ТЫ ХОЧЕШЬ: сохраняем world scale/size
            cardGO.transform.SetParent(slot, true); // НЕ УДАЛЯТЬ

            // Инжектим “свою” доску в карту тайла
            var tileCard = cardGO.GetComponent<DraggableCardTile>();
            if (tileCard != null)
            {
                tileCard.boardView = board;        // BoardRoot_PlayerX
                tileCard.deckManager = this;       // DeckManager_PlayerX (нужно для сброса/выдачи)
                tileCard.Inject(board, this, mainCamera);
            }

            _activeDungeonCards[i] = cardGO;
        }
    }

    // ---------- ВНУТРЕННЯЯ ЛОГИКА ДЛЯ ПОДЗЕМЕЛЬЯ ----------

    private void HandleDungeonCardUsed(GameObject cardGO)
    {
        if (_activeDungeonCards == null)
            return;

        int index = -1;
        for (int i = 0; i < _activeDungeonCards.Length; i++)
        {
            if (_activeDungeonCards[i] == cardGO)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            Destroy(cardGO);
            return;
        }

        Destroy(cardGO);
        _activeDungeonCards[index] = null;

        bool allEmpty = true;
        for (int i = 0; i < _activeDungeonCards.Length; i++)
        {
            if (_activeDungeonCards[i] != null)
            {
                allEmpty = false;
                break;
            }
        }

        if (allEmpty)
        {
            RefillDungeonSlotsIfAllEmpty(force: false);
        }
    }
    public void RefillToFullNow()
    {
        RefillSpellSlotsFillEmpty();
        RefillDungeonSlotsFillEmpty();
    }
    private void RefillSpellSlotsFillEmpty()
    {
        for (int i = 0; i < spellSlots.Length; i++)
        {
            if (_activeSpellCards[i] != null) continue;
            if (_spellDeck.Count == 0) break;

            var entry = _spellDeck[_spellDeck.Count - 1];
            _spellDeck.RemoveAt(_spellDeck.Count - 1);

            DraggableSpellCard card = Instantiate(entry.cardPrefab, spellSlots[i].position, spellSlots[i].rotation);
            card.transform.SetParent(spellSlots[i], true); // ВАЖНО: не ломаем размер

            // инжектим владельца
            card.deckManager = this;
            card.mainCamera = mainCamera != null ? mainCamera : Camera.main;
            card.board = GetTargetBoard();

            _activeSpellCards[i] = card.gameObject;
        }
    }

    private void RefillDungeonSlotsFillEmpty()
    {
        for (int i = 0; i < dungeonSlots.Length; i++)
        {
            if (_activeDungeonCards[i] != null) continue;
            if (_dungeonDeck.Count == 0) break;

            var entry = _dungeonDeck[_dungeonDeck.Count - 1];
            _dungeonDeck.RemoveAt(_dungeonDeck.Count - 1);

            GameObject cardGO = Instantiate(entry.cardPrefab, dungeonSlots[i].position, dungeonSlots[i].rotation);
            cardGO.transform.SetParent(dungeonSlots[i], true); // ВАЖНО: не ломаем размер

            var tileCard = cardGO.GetComponent<DraggableCardTile>();
            if (tileCard != null)
            {
                tileCard.deckManager = this;
                tileCard.boardView = GetTargetBoard();
            }

            _activeDungeonCards[i] = cardGO;
        }
    }
}