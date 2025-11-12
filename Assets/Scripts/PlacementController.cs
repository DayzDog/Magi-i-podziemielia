using UnityEngine;

public class PlacementController : MonoBehaviour
{
    public static PlacementController Instance { get; private set; }

    [Header("Raycast по клеткам")]
    public LayerMask cellMask;              // сюда назначь слой BoardCell

    [Header("Материалы превью (если в TileDefinition не задан свой)")]
    public Material validMat;               // можно ставить
    public Material invalidMat;             // нельзя ставить

    [Header("Управление")]
    public KeyCode rotateCW = KeyCode.E;   // повернуть на +90°
    public KeyCode rotateCCW = KeyCode.Q;   // повернуть на -90°

    // текущее состояние
    private TileDefinition _def;
    private GameObject _previewGO;
    private GameObject _prefab;
    private Quaternion _rot = Quaternion.identity;
    private Camera _cam;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        _cam = Camera.main;
    }

    // Вызываем, когда игрок кликнул по карточке
    public void BeginPlacement(TileDefinition def)
    {
        CancelPlacement();

        _def = def;
        _prefab = def.tilePrefab;
        _rot = Quaternion.identity;

        _previewGO = Instantiate(_prefab);
        _previewGO.name = _prefab.name + "_Preview";

        // превью не должно толкаться/блокировать
        foreach (var c in _previewGO.GetComponentsInChildren<Collider>()) c.enabled = false;

        // если в дефинишене задан свой полупрозрачный материал — применим
        if (_def.previewMaterial != null)
            ApplyMaterialToAll(_previewGO, _def.previewMaterial);
    }

    public void CancelPlacement()
    {
        if (_previewGO) Destroy(_previewGO);
        _previewGO = null;
        _prefab = null;
        _def = null;
        
    }

    void Update()
    {
        if (_previewGO == null) return;

        // вращение превью
        if (Input.GetKeyDown(rotateCW)) _rot *= Quaternion.Euler(0, 90, 0);
        if (Input.GetKeyDown(rotateCCW)) _rot *= Quaternion.Euler(0, -90, 0);

        // луч из камеры под курсор
        var ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 500f, cellMask, QueryTriggerInteraction.Ignore))
        {
            // нашли клетку
            var cell = hit.collider.GetComponentInParent<BoardCell>();
            if (cell != null)
            {
                bool canPlace = !cell.occupied;

                // позиция превью = точка привязки клетки + маленький подъём
                Vector3 pos = cell.WorldSnapPoint + _def.previewYOffset;
                _previewGO.transform.SetPositionAndRotation(pos, _rot);

                // если в TileDefinition нет своего материала ghost — подсветим валидность
                if (_def.previewMaterial == null)
                    ApplyMaterialToAll(_previewGO, canPlace ? validMat : invalidMat);

                // поставить
                if (canPlace && Input.GetMouseButtonDown(0))
                {
                    var obj = Instantiate(_prefab, cell.WorldSnapPoint, _rot);
                    foreach (var c in obj.GetComponentsInChildren<Collider>()) c.enabled = true;

                    // прикрепим к клетке для удобства
                    obj.transform.SetParent(cell.transform, worldPositionStays: true);
                    cell.SetOccupant(obj.transform);

                    // хотим ставить серией — оставляем превью.
                    // если нужно одноразово — раскомментируй:
                    // CancelPlacement();
                }
            }
        }

        // отменить размещение
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            CancelPlacement();
    }

    private void ApplyMaterialToAll(GameObject go, Material m)
    {
        if (m == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }
}


