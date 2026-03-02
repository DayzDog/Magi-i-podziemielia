using UnityEngine;

public enum CardDiscardType { Spells, Dungeon, Both }

public class CardDiscardZone : MonoBehaviour
{
    public CardDiscardType type = CardDiscardType.Both;
    public CardDeckManager deckManager;
    public int ownerPlayerId = 1; // 1 = P1, 2 = P2
}