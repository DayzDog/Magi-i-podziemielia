using UnityEngine;

[CreateAssetMenu(menuName = "Tiles/Tile Definition")]
public class TileDefinition : ScriptableObject
{
    [Header("Prefab реального тайла")]
    public GameObject tilePrefab;

    [Header("Как показывать 'превью'")]
    public Material previewMaterial;         // полупрозрачный материал для ghost-модели
    public Vector3 previewYOffset = new Vector3(0, 0.01f, 0); // приподнять превью
}

