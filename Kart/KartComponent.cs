using Fusion;
using UnityEngine;

public class KartComponent : NetworkBehaviour 
{
    public KartEntity Kart { get; private set; }

    public virtual void Init(KartEntity kart) 
    {
        Kart = kart;
    }
    
    /// <summary>
    /// Called on the tick that the race has started. This method is tick-aligned.
    /// </summary>
    public virtual void OnRaceStart() { }
    
    /// <summary>
    /// Called when this kart has crossed the finish line. This method is tick-aligned.
    /// </summary>
    public virtual void OnLapCompleted(int lap, bool isFinish) { }
    
    /// <summary>
    /// Called when an item has been picked up. This method is tick-aligned.
    /// Original method for compatibility
    /// </summary>
    public virtual void OnEquipItem(Powerup powerup, float timeUntilCanUse) { }
    
    /// <summary>
    /// Called when an item has been picked up with slot index. This method is tick-aligned.
    /// New overload for slot system
    /// </summary>
    public virtual void OnEquipItem(Powerup powerup, float timeUntilCanUse, int slotIndex) 
    { 
        // Call the original method for backward compatibility
        OnEquipItem(powerup, timeUntilCanUse);
    }
}