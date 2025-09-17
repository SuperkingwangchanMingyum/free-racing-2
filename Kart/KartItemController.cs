using Fusion;
using UnityEngine;

public class KartItemController : KartComponent 
{
    public float equipItemTimeout = 3f;
    public float boosterEquipTimeout = 2f; // 부스터는 더 짧은 쿨다운
    public float useItemTimeout = 2.5f;

    [Networked]
    public TickTimer EquipCooldown { get; set; }
    
    [Networked]
    public TickTimer PrimaryEquipCooldown { get; set; }
    
    [Networked]
    public TickTimer SecondaryEquipCooldown { get; set; }
    
    [Networked]
    public TickTimer BoosterEquipCooldown { get; set; }
    
    // 기존 호환성 유지
    public bool CanUseItem => Kart.HeldItemIndex != -1 && EquipCooldown.ExpiredOrNotRunning(Runner);
    
    // 각 슬롯별 사용 가능 여부
    public bool CanUsePrimaryItem => Kart.PrimaryItemIndex != -1 && PrimaryEquipCooldown.ExpiredOrNotRunning(Runner);
    public bool CanUseSecondaryItem => Kart.SecondaryItemIndex != -1 && SecondaryEquipCooldown.ExpiredOrNotRunning(Runner);
    public bool CanUseBoosterItem => Kart.BoosterItemIndex != -1 && BoosterEquipCooldown.ExpiredOrNotRunning(Runner);

    // 기존 메소드 - 호환성 유지
    public override void OnEquipItem(Powerup powerup, float timeUntilCanUse) 
    {
        base.OnEquipItem(powerup, timeUntilCanUse);
        EquipCooldown = TickTimer.CreateFromSeconds(Runner, equipItemTimeout);
        PrimaryEquipCooldown = TickTimer.CreateFromSeconds(Runner, equipItemTimeout);
    }
    
    // 새로운 오버로드 메소드 - 슬롯 인덱스 지원
    public override void OnEquipItem(Powerup powerup, float timeUntilCanUse, int slotIndex) 
    {
        base.OnEquipItem(powerup, timeUntilCanUse, slotIndex);

        switch(slotIndex) 
        {
            case 0: // Primary slot
                PrimaryEquipCooldown = TickTimer.CreateFromSeconds(Runner, equipItemTimeout);
                break;
            case 1: // Secondary slot
                SecondaryEquipCooldown = TickTimer.CreateFromSeconds(Runner, equipItemTimeout);
                break;
            case 2: // Booster slot
                BoosterEquipCooldown = TickTimer.CreateFromSeconds(Runner, boosterEquipTimeout);
                break;
            default: // 기본값 (호환성)
                EquipCooldown = TickTimer.CreateFromSeconds(Runner, equipItemTimeout);
                PrimaryEquipCooldown = TickTimer.CreateFromSeconds(Runner, equipItemTimeout);
                break;
        }
    }

    // Shift키로 순차적으로 아이템 사용
    public void UseNextAvailableItem()
    {
        // 첫 번째 슬롯부터 확인
        if (CanUsePrimaryItem)
        {
            UsePrimaryItem();
            return;
        }
        
        // 첫 번째가 비어있으면 두 번째 확인
        if (CanUseSecondaryItem)
        {
            UseSecondaryItem();
            return;
        }
        
        // 두 번째도 비어있으면 세 번째 확인
        if (CanUseBoosterItem)
        {
            UseBoosterItem();
            return;
        }
        
        // 모든 슬롯이 비어있거나 쿨다운 중
        if (!Runner.IsForward) return;
        Kart.Audio.PlayHorn();
    }
    
    // 기존 메소드 - 호환성 유지
    public void UseItem() 
    {
        // Shift키로 호출되면 순차적 사용
        UseNextAvailableItem();
    }
    
    // 첫 번째 일반 슬롯 아이템 사용
    public void UsePrimaryItem() 
    {
        if (!CanUsePrimaryItem) 
        {
            if (!Runner.IsForward) return;
            Kart.Audio.PlayHorn();
        } 
        else 
        {
            Kart.PrimaryItem.Use(Runner, Kart);
            Kart.PrimaryItemIndex = -1;
            Kart.HeldItemIndex = -1; // 호환성
        }
    }
    
    // 두 번째 일반 슬롯 아이템 사용
    public void UseSecondaryItem() 
    {
        if (!CanUseSecondaryItem) 
        {
            if (!Runner.IsForward) return;
            Kart.Audio.PlayHorn();
        } 
        else 
        {
            Kart.SecondaryItem.Use(Runner, Kart);
            Kart.SecondaryItemIndex = -1;
        }
    }
    
    // 부스터 슬롯 아이템 사용
    public void UseBoosterItem() 
    {
        if (!CanUseBoosterItem) 
        {
            if (!Runner.IsForward) return;
            Kart.Audio.PlayHorn();
        } 
        else 
        {
            Kart.BoosterItem.Use(Runner, Kart);
            Kart.BoosterItemIndex = -1;
        }
    }
}