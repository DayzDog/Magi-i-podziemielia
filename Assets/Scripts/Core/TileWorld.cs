using UnityEngine;

public class TileWorld : MonoBehaviour
{
    [HideInInspector]
    public Board3DView board;

    // координаты клетки, в которую поставлен тайл
    public int x;
    public int y;

    // true только для стартового тайла
    public bool isStart;

    // флаг: этот тайл сейчас подсвечен как возможная цель хода
    [HideInInspector]
    public bool canMoveTarget;

    private void OnMouseDown()
    {
        if (board != null)
        {
            board.OnTileClicked(this);
        }
    }
}