using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
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
/// </summary>
public class CardDeckManager : MonoBehaviour
{
    [Header("Связь с доской (необязательно, просто для удобства)")]
    public Board3DView board;

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
        [Tooltip("Префаб карты тайла подземелья (на руках). На нём висит твой скрипт перетаскивания тайла.")]
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

    // ---------- ВНУТРЕННЕЕ СОСТОЯНИЕ ДЕКОВ ----------

    // реальные колоды (список «карт») после разворачивания по count
    private readonly List<SpellCardEntry> _spellDeck = new List<SpellCardEntry>();
    private readonly List<DungeonCardEntry> _dungeonDeck = new List<DungeonCardEntry>();

    // какие конкретные объекты карт сейчас лежат на столе в слотах
    private GameObject[] _activeSpellCards;
    private GameObject[] _activeDungeonCards;

    private void Awake()
    {
        if (board == null)
            board = FindFirstObjectByType<Board3DView>();

        BuildSpellDeck();
        BuildDungeonDeck();

        // массивы активных карт по количеству слотов
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
    /// Вызывается картой заклинания, когда заклинание УСПЕШНО применено.
    /// </summary>
    public void OnSpellCardUsed(DraggableSpellCard card)
    {
        if (card == null) return;
        HandleSpellCardUsed(card.gameObject);
    }

    /// <summary>
    /// Вызывается картой подземелья, когда тайл УСПЕШНО поставлен.
    /// Просто передаём сюда gameObject карты.
    /// </summary>
    public void OnDungeonCardUsed(GameObject cardGO)
    {
        if (cardGO == null) return;
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

        // по твоему правилу: НОВЫЕ карты выдаём только когда
        // обе карты магии уже использованы (все слоты пустые)
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
            Debug.Log("[Deck] Колода заклинаний пуста, новых карт магии не выдаём.");
            return;
        }

        if (!force)
        {
            // проверяем, правда ли все слоты пустые
            for (int i = 0; i < _activeSpellCards.Length; i++)
            {
                if (_activeSpellCards[i] != null)
                    return; // ещё есть карты, не раздаём новые
            }
        }

        // пробегаем по слотам и наполняем их пока есть карты
        for (int i = 0; i < spellSlots.Length; i++)
        {
            if (_spellDeck.Count == 0)
                break;

            var slot = spellSlots[i];
            if (slot == null)
                continue;

            if (_activeSpellCards[i] != null)
                continue; // на всякий случай

            // берём последнюю карту из колоды (она уже перетасована)
            var entry = _spellDeck[_spellDeck.Count - 1];
            _spellDeck.RemoveAt(_spellDeck.Count - 1);

            // ВАЖНО: спавним БЕЗ родителя, потом задаём масштаб и родителя
            DraggableSpellCard card = Instantiate(
                entry.cardPrefab,
                slot.position,
                slot.rotation);

            // масштабирем как префаб (иначе может наследоваться странный scale)
            card.transform.localScale = entry.cardPrefab.transform.localScale;

            // привязываем к слоту, но сохраняем мировую позицию/масштаб
            card.transform.SetParent(slot, worldPositionStays: true);

            // настроим ссылку на DeckManager, если не задана
            if (card.deckManager == null)
                card.deckManager = this;

            _activeSpellCards[i] = card.gameObject;
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

    private void RefillDungeonSlotsIfAllEmpty(bool force)
    {
        if (dungeonSlots == null || dungeonSlots.Length == 0)
            return;

        if (_dungeonDeck.Count == 0)
        {
            Debug.Log("[Deck] Колода подземелья пуста, новых карт путей не выдаём.");
            return;
        }

        if (!force)
        {
            for (int i = 0; i < _activeDungeonCards.Length; i++)
            {
                if (_activeDungeonCards[i] != null)
                    return;
            }
        }

        for (int i = 0; i < dungeonSlots.Length; i++)
        {
            if (_dungeonDeck.Count == 0)
                break;

            var slot = dungeonSlots[i];
            if (slot == null)
                continue;

            if (_activeDungeonCards[i] != null)
                continue;

            var entry = _dungeonDeck[_dungeonDeck.Count - 1];
            _dungeonDeck.RemoveAt(_dungeonDeck.Count - 1);

            // ТАК ЖЕ: спавним без родителя, потом задаём scale и parent
            GameObject cardGO = Instantiate(
                entry.cardPrefab,
                slot.position,
                slot.rotation);

            cardGO.transform.localScale = entry.cardPrefab.transform.localScale;
            cardGO.transform.SetParent(slot, worldPositionStays: true);

            // На всякий случай пробрасываем boardView, если в префабе не проставлен
            var dragTile = cardGO.GetComponent<DraggableCardTile>();
            if (dragTile != null && dragTile.boardView == null)
            {
                dragTile.boardView = board;
            }

            _activeDungeonCards[i] = cardGO;
        }
    }
}
