using UnityEngine;

public class TurnCrystalsSet : MonoBehaviour
{
    public TurnCrystal[] crystals;

    public void ResetAll()
    {
        if (crystals == null) return;
        foreach (var c in crystals)
            if (c != null) c.ResetCrystal();
    }
}