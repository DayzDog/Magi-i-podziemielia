using UnityEngine;

public class TileConnectionsView : MonoBehaviour
{
    [Header("Сегменты для визуала (Path = проход, Wall = стена)")]
    public GameObject upPath;
    public GameObject upWall;

    public GameObject rightPath;
    public GameObject rightWall;

    public GameObject downPath;
    public GameObject downWall;

    public GameObject leftPath;
    public GameObject leftWall;

    /// <summary>
    /// Применяем сокеты: где true — показываем проход, где false — стену.
    /// </summary>
    public void ApplySockets(Sockets s)
    {
        SetPair(upPath, upWall, s.Up);
        SetPair(rightPath, rightWall, s.Right);
        SetPair(downPath, downWall, s.Down);
        SetPair(leftPath, leftWall, s.Left);
    }

    private void SetPair(GameObject pathObj, GameObject wallObj, bool isPath)
    {
        if (pathObj != null)
            pathObj.SetActive(isPath);

        if (wallObj != null)
            wallObj.SetActive(!isPath);
    }
}