using UnityEngine;

[CreateAssetMenu(menuName = "Tiles/Tile Definition")]
public class TileDefinition : ScriptableObject
{
    public string id;
    public GameObject tilePrefab;   // модель тайла, которую ставим на поле
    public Material previewMaterial; // полупрозрачный материал для “превью” (необязательно)
    public Vector3 previewYOffset = new Vector3(0, 0.01f, 0); // чуть приподнять “превью”
}
