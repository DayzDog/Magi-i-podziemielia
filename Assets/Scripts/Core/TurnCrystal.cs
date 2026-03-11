using UnityEngine;

public class TurnCrystal : MonoBehaviour
{
    public enum CrystalAction
    {
        SkipEnemySpellPhase, // 1) пропустить фазу атаки магией (“ќЋ№ ќ EnemySpellOpt)
        SkipPlace,           // 2) пропустить выкладку тайла (в фазе 3 галочек)
        SkipSpell,           // 3) пропустить магию (в фазе 3 галочек)
        SkipMove             // 4) пропустить движение (в фазе 3 галочек)
    }

    [Header("Action")]
    public CrystalAction action;

    [Header("Visuals")]
    public GameObject intactModel;  // целый
    public GameObject brokenModel;  // сломанный

    [SerializeField] private bool used;

    private void OnEnable()
    {
        ApplyVisual();
    }

    public void ResetCrystal()
    {
        used = false;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (intactModel != null) intactModel.SetActive(!used);
        if (brokenModel != null) brokenModel.SetActive(used);
    }

    private void OnMouseDown()
    {
        if (used) return;

        var tm = TurnManager.I;
        if (tm == null) return;

        bool ok = false;

        switch (action)
        {
            case CrystalAction.SkipEnemySpellPhase:
                ok = tm.Crystal_SkipEnemySpellPhase();
                break;

            case CrystalAction.SkipPlace:
                ok = tm.Crystal_SkipOwnerPlace();
                break;

            case CrystalAction.SkipSpell:
                ok = tm.Crystal_SkipOwnerSpell();
                break;

            case CrystalAction.SkipMove:
                ok = tm.Crystal_SkipOwnerMove();
                break;
        }

        if (ok)
        {
            used = true;
            ApplyVisual();
        }
    }
}