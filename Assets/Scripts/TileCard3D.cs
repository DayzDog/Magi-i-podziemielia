using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // новый Input System
#endif

/// Навеси на 3D-объект карточки (у него должен быть Collider).
/// При наведении показывает превью тайла над карточкой.
/// По клику запускает размещение в PlacementController.
public class TileCard3D : MonoBehaviour
{
    [Header("Что за тайл эта карточка spawns")]
    public TileDefinition definition;

    [Header("Где рисовать мини-превью над карточкой")]
    public Transform previewAnchor;              // необязателен; если не задан — используем transform
    public Vector3 hoverPreviewOffset = new Vector3(0, 0.02f, 0);

    private GameObject _hoverPreview;
    private bool _isHovered;

    void Reset()
    {
        // чтобы не забыть коллайдер
        if (GetComponent<Collider>() == null)
            gameObject.AddComponent<BoxCollider>();
    }

    // ====== Наведение (работает без UI, через встроенный OnMouse...) ======
    void OnMouseEnter()
    {
        _isHovered = true;
        ShowHoverPreview();
    }

    void OnMouseExit()
    {
        _isHovered = false;
        HideHoverPreview();
    }

    // Вариант клика через старый Input
    void OnMouseDown()
    {
        TryBeginPlacement();
    }

    // Дополнительно: клик через новый Input System (на случай, если OnMouseDown не сработает в проекте)
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (_isHovered && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryBeginPlacement();
        }
#endif
    }

    private void TryBeginPlacement()
    {
        if (definition == null)
        {
            Debug.LogError("[TileCard3D] На карточке не назначен TileDefinition: " + name);
            return;
        }
        HideHoverPreview(); // убираем висящее превью на карточке
        if (PlacementController.Instance == null)
        {
            Debug.LogError("[TileCard3D] PlacementController.Instance не найден в сцене");
            return;
        }
        PlacementController.Instance.BeginPlacement(definition);
    }

    private void ShowHoverPreview()
    {
        if (_hoverPreview != null || definition == null || definition.tilePrefab == null) return;

        Transform anchor = previewAnchor != null ? previewAnchor : transform;
        _hoverPreview = Instantiate(definition.tilePrefab, anchor);
        _hoverPreview.name = definition.tilePrefab.name + "_HoverPreview";
        _hoverPreview.transform.localPosition = hoverPreviewOffset;
        _hoverPreview.transform.localRotation = Quaternion.identity;

        foreach (var c in _hoverPreview.GetComponentsInChildren<Collider>()) c.enabled = false;

        // полупрозрачный материал для “ghost”
        if (definition.previewMaterial != null)
            ApplyMaterialToAll(_hoverPreview, definition.previewMaterial);
    }

    private void HideHoverPreview()
    {
        if (_hoverPreview) Destroy(_hoverPreview);
        _hoverPreview = null;
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
