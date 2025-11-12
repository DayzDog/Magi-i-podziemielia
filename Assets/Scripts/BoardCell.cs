using UnityEngine;


public class BoardCell : MonoBehaviour
{
    [Tooltip("Необязательно: логические координаты для отладки/сохранений")]
    public Vector2Int coord;

    [Tooltip("Локальная точка привязки внутри клетки (0,0,0 = центр)")]
    public Vector3 snapPointLocal = Vector3.zero;

    [HideInInspector] public bool occupied;
    [HideInInspector] public Transform occupant;

    public Vector3 WorldSnapPoint => transform.TransformPoint(snapPointLocal);

    public void SetOccupant(Transform t)
    {
        occupant = t;
        occupied = t != null;
    }
}
