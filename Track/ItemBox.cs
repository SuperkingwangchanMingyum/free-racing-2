using UnityEngine;
using Fusion;
using Random = UnityEngine.Random;

public class ItemBox : NetworkBehaviour, ICollidable {
    
    public GameObject model;
    public ParticleSystem breakParticle;
    public float cooldown = 5f;
    public Transform visuals;

    [Networked] public KartEntity Kart { get; set; }
    [Networked] public TickTimer DisabledTimer { get; set; }
    
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(Kart):
                    OnKartChanged(this);
                    break;
            }
        }
    }

    public bool Collide(KartEntity kart) {
        if (kart != null && DisabledTimer.ExpiredOrNotRunning(Runner)) {
            Kart = kart;
            DisabledTimer = TickTimer.CreateFromSeconds(Runner, cooldown);
            
            // 스마트 슬롯 할당 시스템
            bool itemGiven = TryGiveItemToKart(kart);
            
            // 아이템을 받지 못했다면 (모든 슬롯이 차있음)
            if (!itemGiven && Object.HasInputAuthority) {
                AudioManager.Play("itemWasteSFX", AudioManager.MixerTarget.SFX, transform.position);
            }
        }

        return true;
    }
    
    private bool TryGiveItemToKart(KartEntity kart)
    {
        var powerUp = GetRandomPowerup();
        
        // 마리오카트 스타일: 순서대로 빈 슬롯 찾기
        
        // 1. 첫 번째 슬롯 확인 - 비어있을 때만
        if (kart.PrimaryItem == null)
        {
            kart.SetPrimaryItem(powerUp);
            return true;
        }
        // 슬롯 1이 차있으면 절대 건드리지 않음
        
        // 2. 두 번째 슬롯 확인
        if (kart.SecondaryItem == null)
        {
            kart.SetSecondaryItem(powerUp);
            return true;
        }
        
        // 3. 세 번째 슬롯 확인
        if (kart.BoosterItem == null)
        {
            kart.SetBoosterItem(powerUp);
            return true;
        }
        
        // 모든 슬롯이 차있음
        return false;
    }

    private static void OnKartChanged(ItemBox changed) { changed.OnKartChanged(); }
    
    private void OnKartChanged() {
        visuals.gameObject.SetActive(Kart == null);

        if (Kart == null)
            return;

        // 아이템 획득 성공 여부에 따라 다른 사운드
        bool hasEmptySlot = Kart.PrimaryItem == null || 
                           Kart.SecondaryItem == null || 
                           Kart.BoosterItem == null;
        
        AudioManager.PlayAndFollow(
            hasEmptySlot ? "itemCollectSFX" : "itemWasteSFX",
            transform,
            AudioManager.MixerTarget.SFX
        );

        breakParticle.Play();
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        
        if (DisabledTimer.ExpiredOrNotRunning(Runner) && Kart != null) {
            Kart = null;
        }
    }

    private int GetRandomPowerup() {
        var powerUps = ResourceManager.Instance.powerups;
        var seed = Runner.Tick;
        
        Random.InitState(seed);
        
        return Random.Range(0, powerUps.Length);
    }
}