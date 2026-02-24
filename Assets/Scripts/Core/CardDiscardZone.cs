using UnityEngine;

public enum CardDiscardType
{
    Spells,     // только карты магии
    Dungeon,    // только карты подземелья
    Both        // принимает всё
}

public class CardDiscardZone : MonoBehaviour
{
    public CardDiscardType type = CardDiscardType.Both;
}