using UnityEngine;

public class PlacementController : MonoBehaviour
{
    [Header("Диагностика")]
    [SerializeField] private bool autoStartOnPlay = false;   // ← галочку поставим в инспекторе
    [SerializeField] private TileDefinition autoStartDef;     // ← сюда брось любой TileDefinition
    public static PlacementController Instance { get; private set; }

    [Header("Raycast по клеткам")]
    public LayerMask cellMask;              // слой BoardCell

    [Header("Материалы превью (если в TileDefinition не задан свой)")]
    public Material validMat;               // можно ставить
    public Material invalidMat;             // нельзя ставить

    [Header("Управление")]
    public KeyCode rotateCW = KeyCode.E;   // +90°
    public KeyCode rotateCCW = KeyCode.Q;   // -90°

    [Header("Какая камера считывает курсор")]
    [SerializeField] private Camera inputCamera; // перетащи сюда свою камеру в инспекторе (или пометь её MainCamera)

    // текущее состояние
    private TileDefinition _def;
    private GameObject _previewGO;
    private GameObject _prefab;
    private Quaternion _rot = Quaternion.identity;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (inputCamera == null) inputCamera = Camera.main;
        if (inputCamera == null)
            Debug.LogError("[Placement] Не назначена камера! Перетащи камеру в поле 'Input Camera' у PlacementController.");
        else
            Debug.Log("[Placement] Используем камеру: " + inputCamera.name);
    }

    void Update()
    {
        if (_previewGO == null || inputCamera == null) return;

        // вращение превью
        if (Input.GetKeyDown(rotateCW)) _rot *= Quaternion.Euler(0, 90, 0);
        if (Input.GetKeyDown(rotateCCW)) _rot *= Quaternion.Euler(0, -90, 0);

        // луч из камеры под курсор
        Ray ray = inputCamera.ScreenPointToRay(Input.mousePosition);
        Debug.DrawRay(ray.origin, ray.direction * 100f, Color.cyan);

        if (Physics.Raycast(ray, out var hit, 500f, cellMask, QueryTriggerInteraction.Ignore))
        {
            var cell = hit.collider.GetComponentInParent<BoardCell>();
            if (cell != null)
            {
                bool canPlace = !cell.occupied;

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

                    // привязать к клетке — удобно для уборки/сохранений
                    obj.transform.SetParent(cell.transform, worldPositionStays: true);
                    cell.SetOccupant(obj.transform);
                }
            }
        }

        // отмена
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            CancelPlacement();
    }
    void Start()
    {
        if (autoStartOnPlay && autoStartDef != null)
        {
            Debug.Log("[Placement] AutoStart BeginPlacement(" + autoStartDef.name + ")");
            BeginPlacement(autoStartDef);
        }
        else if (autoStartOnPlay)
        {
            Debug.LogError("[Placement] AutoStart включён, но autoStartDef пуст.");
        }

        if ((cellMask.value) == 0)
            Debug.LogWarning("[Placement] cellMask пуст. Назначь слой BoardCell.");
    }


    // ==== ПУБЛИЧНЫЕ API ====

    // вызывает карточка, когда игрок кликнул по ней
    public void BeginPlacement(TileDefinition def)
    {
        CancelPlacement();

        _def = def;
        _prefab = def.tilePrefab;
        _rot = Quaternion.identity;

        if (_prefab == null)
        {
            Debug.LogError("[Placement] tilePrefab в TileDefinition не задан!");
            return;
        }

        _previewGO = Instantiate(_prefab);
        _previewGO.name = _prefab.name + "_Preview";

        // превью не должно мешать кликам
        foreach (var c in _previewGO.GetComponentsInChildren<Collider>()) c.enabled = false;

        // применим ghost-материал, если указан в дефинишене
        if (_def.previewMaterial != null)
            ApplyMaterialToAll(_previewGO, _def.previewMaterial);

        Debug.Log("[Placement] Начали размещение: " + _prefab.name);
    }

    public void CancelPlacement()
    {
        if (_previewGO) Destroy(_previewGO);
        _previewGO = null;
        _prefab = null;
        _def = null;
    }

    // ==== ВСПОМОГАТЕЛЬНОЕ ====

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
