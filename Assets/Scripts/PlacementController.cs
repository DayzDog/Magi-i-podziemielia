using Mono.Cecil;
using UnityEngine;

public class PlacementController : MonoBehaviour
{
    public static PlacementController Instance { get; private set; }

    [Header("Raycast")]
    public LayerMask cellMask;       // слой ваших клеток (BoardCell)

    [Header("Preview")]
    public Material validMat;        // материал превью "можно ставить"
    public Material invalidMat;      // материал превью "нельзя ставить"
    public Vector3 previewYOffset = new Vector3(0, 0.01f, 0);

    [Header("Input")]
    public KeyCode rotateCW = KeyCode.E;
    public KeyCode rotateCCW = KeyCode.Q;

    TileDefinition _def;
    GameObject _previewGO;
    GameObject _prefab;
    Quaternion _rot = Quaternion.identity;
    Camera _cam;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        _cam = Camera.main;
    }

    public void BeginPlacement(TileDefinition def)
    {
        CancelPlacement();

        _def = def;
        _prefab = def.tilePrefab;
        _rot = Quaternion.identity;

        _previewGO = Instantiate(_prefab);
        _previewGO.name = _prefab.name + "_Preview";
        foreach (var c in _previewGO.GetComponentsInChildren<Collider>()) c.enabled = false;

        // применим ghost-материал, если указан в def; иначе будем переключать valid/invalidMat
        if (def.previewMaterial != null)
            ApplyMaterialToAll(_previewGO, def.previewMaterial);
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

        if (Input.GetKeyDown(rotateCW)) _rot *= Quaternion.Euler(0, 90, 0);
        if (Input.GetKeyDown(rotateCCW)) _rot *= Quaternion.Euler(0, -90, 0);

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 500f, cellMask, QueryTriggerInteraction.Ignore))
        {
            var cell = hit.collider.GetComponentInParent<BoardCell>();
            if (cell != null)
            {
                bool canPlace = !cell.occupied;

                // позиция превью = снап-точка клетки + небольшой подъём
                Vector3 targetPos = cell.WorldSnapPoint + previewYOffset;
                _previewGO.transform.SetPositionAndRotation(targetPos, _rot);

                // подсветка валидности (если в TileDefinition не задан свой материал)
                if (_def.previewMaterial == null)
                    ApplyMaterialToAll(_previewGO, canPlace ? validMat : invalidMat);

                // поставить
                if (canPlace && Input.GetMouseButtonDown(0))
                {
                    var obj = Instantiate(_prefab, cell.WorldSnapPoint, _rot);
                    foreach (var c in obj.GetComponentsInChildren<Collider>()) c.enabled = true;

                    // привяжем к иерархии клетки (удобно для уборки/сохранений)
                    obj.transform.SetParent(cell.transform, worldPositionStays: true);
                    cell.SetOccupant(obj.transform);

                    // если нужно ставить несколько подряд, оставьте превью; иначе:
                    // CancelPlacement();
                }
            }
        }

        // отмена
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            CancelPlacement();
    }

    void ApplyMaterialToAll(GameObject go, Material m)
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
