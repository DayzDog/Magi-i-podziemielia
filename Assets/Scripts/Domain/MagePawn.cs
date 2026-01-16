using UnityEngine;

public class MagePawn : MonoBehaviour
{
    [HideInInspector]
    public Board3DView board;

    private void OnMouseDown()
    {
        if (board != null)
        {
            board.OnMageClicked();
        }
    }
}