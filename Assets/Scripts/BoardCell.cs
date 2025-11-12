using UnityEngine;

public class BoardCell : MonoBehaviour
{
    [Tooltip("Ќеоб€зательно: логические координаты клетки дл€ отладки/сохранений")]
    public Vector2Int coord;

    [Tooltip("“очка прив€зки относительно pivot клетки (0,0,0 = центр)")]
    public Vector3 snapPointLocal = Vector3.zero;

    [HideInInspector] public bool occupied;
    [HideInInspector] public Transform occupant;

    // ћирова€ точка прив€зки (куда ставим тайл)
    public Vector3 WorldSnapPoint => transform.TransformPoint(snapPointLocal);

    public void SetOccupant(Transform t)
    {
        occupant = t;
        occupied = t != null;
    }
}
