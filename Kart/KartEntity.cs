using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class KartEntity : KartComponent
{
    public static event Action<KartEntity> OnKartSpawned;
    public static event Action<KartEntity> OnKartDespawned;

    // 아이템 변경 이벤트
    public event Action<int> OnHeldItemChanged;
    public event Action<int> OnPrimaryItemChanged;
    public event Action<int> OnSecondaryItemChanged;
    public event Action<int> OnBoosterItemChanged;
    public event Action<int> OnCoinCountChanged;
    
    // 컴포넌트 참조
    public KartAnimator Animator { get; private set; }
    public KartCamera Camera { get; private set; }
    public KartController Controller { get; private set; }
    public KartInput Input { get; private set; }
    public KartLapController LapController { get; private set; }
    public KartAudio Audio { get; private set; }
    public GameUI Hud { get; private set; }
    public KartItemController Items { get; private set; }
    public NetworkRigidbody3D Rigidbody { get; private set; }

    // 아이템 슬롯 - 기존 호환성 유지
    public Powerup HeldItem =>
        HeldItemIndex == -1
            ? null
            : ResourceManager.Instance.powerups[HeldItemIndex];

    // 첫 번째 일반 슬롯
    public Powerup PrimaryItem =>
        PrimaryItemIndex == -1
            ? null
            : ResourceManager.Instance.powerups[PrimaryItemIndex];

    // 두 번째 일반 슬롯
    public Powerup SecondaryItem =>
        SecondaryItemIndex == -1
            ? null
            : ResourceManager.Instance.powerups[SecondaryItemIndex];

    // 부스터 전용 슬롯
    public Powerup BoosterItem =>
        BoosterItemIndex == -1
            ? null
            : ResourceManager.Instance.powerups[BoosterItemIndex];

    [Networked]
    public int HeldItemIndex { get; set; } = -1;

    [Networked]
    public int PrimaryItemIndex { get; set; } = -1;

    [Networked]
    public int SecondaryItemIndex { get; set; } = -1;

    [Networked]
    public int BoosterItemIndex { get; set; } = -1;

    [Networked]
    public int CoinCount { get; set; }

    public Transform itemDropNode;

    private bool _despawned;
    private ChangeDetector _changeDetector;

    // 정적 콜백 메소드들
    private static void OnHeldItemIndexChangedCallback(KartEntity changed)
    {
        changed.OnHeldItemChanged?.Invoke(changed.HeldItemIndex);

        if (changed.HeldItemIndex != -1)
        {
            foreach (var behaviour in changed.GetComponentsInChildren<KartComponent>())
                behaviour.OnEquipItem(changed.HeldItem, 3f);
        }
    }

    private static void OnPrimaryItemChangedCallback(KartEntity changed)
    {
        changed.OnPrimaryItemChanged?.Invoke(changed.PrimaryItemIndex);

        if (changed.PrimaryItemIndex != -1)
        {
            foreach (var behaviour in changed.GetComponentsInChildren<KartComponent>())
                behaviour.OnEquipItem(changed.PrimaryItem, 3f);
        }
    }

    private static void OnSecondaryItemChangedCallback(KartEntity changed)
    {
        changed.OnSecondaryItemChanged?.Invoke(changed.SecondaryItemIndex);

        if (changed.SecondaryItemIndex != -1)
        {
            foreach (var behaviour in changed.GetComponentsInChildren<KartComponent>())
                behaviour.OnEquipItem(changed.SecondaryItem, 3f);
        }
    }

    private static void OnBoosterItemChangedCallback(KartEntity changed)
    {
        changed.OnBoosterItemChanged?.Invoke(changed.BoosterItemIndex);

        if (changed.BoosterItemIndex != -1)
        {
            foreach (var behaviour in changed.GetComponentsInChildren<KartComponent>())
                behaviour.OnEquipItem(changed.BoosterItem, 2f);
        }
    }

    private static void OnCoinCountChangedCallback(KartEntity changed)
    {
        changed.OnCoinCountChanged?.Invoke(changed.CoinCount);
    }

    private void Awake()
    {
        // Set references before initializing all components
        Animator = GetComponentInChildren<KartAnimator>();
        Camera = GetComponent<KartCamera>();
        Controller = GetComponent<KartController>();
        Input = GetComponent<KartInput>();
        LapController = GetComponent<KartLapController>();
        Audio = GetComponentInChildren<KartAudio>();
        Items = GetComponent<KartItemController>();
        Rigidbody = GetComponent<NetworkRigidbody3D>();

        // Initializes all KartComponents on or under the Kart prefab
        var components = GetComponentsInChildren<KartComponent>();
        foreach (var component in components) component.Init(this);
    }

    public static readonly List<KartEntity> Karts = new List<KartEntity>();

    public override void Spawned()
    {
        base.Spawned();
        
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        
        if (Object.HasInputAuthority)
        {
            // Create HUD
            Hud = Instantiate(ResourceManager.Instance.hudPrefab);
            Hud.Init(this);

            Instantiate(ResourceManager.Instance.nicknameCanvasPrefab);
        }

        Karts.Add(this);
        OnKartSpawned?.Invoke(this);
    }
    
    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(PrimaryItemIndex):
                    OnPrimaryItemChangedCallback(this);
                    break;
                case nameof(HeldItemIndex):
                    // HeldItemIndex는 PrimaryItemIndex와 동기화되므로
                    // PrimaryItemIndex와 값이 다를 때만 호출
                    if (HeldItemIndex != PrimaryItemIndex)
                    {
                        OnHeldItemIndexChangedCallback(this);
                    }
                    break;
                case nameof(SecondaryItemIndex):
                    OnSecondaryItemChangedCallback(this);
                    break;
                case nameof(BoosterItemIndex):
                    OnBoosterItemChangedCallback(this);
                    break;
                case nameof(CoinCount):
                    OnCoinCountChangedCallback(this);
                    break;
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        Karts.Remove(this);
        _despawned = true;
        OnKartDespawned?.Invoke(this);
    }

    private void OnDestroy()
    {
        Karts.Remove(this);
        if (!_despawned)
        {
            OnKartDespawned?.Invoke(this);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.TryGetComponent(out ICollidable collidable))
        {
            collidable.Collide(this);
        }
    }

    // 기존 메소드 - 호환성 유지 (순서대로 빈 슬롯에 할당)
    public bool SetHeldItem(int index)
    {
        // 첫 번째 빈 슬롯에 할당
        if (PrimaryItem == null)
        {
            return SetPrimaryItem(index);
        }
        else if (SecondaryItem == null)
        {
            return SetSecondaryItem(index);
        }
        else if (BoosterItem == null)
        {
            return SetBoosterItem(index);
        }
        
        return false; // 모든 슬롯이 차있음
    }

    // 첫 번째 일반 슬롯에 아이템 설정
    public bool SetPrimaryItem(int index)
    {
        // 이미 아이템이 있으면 절대 변경 불가
        if (PrimaryItem != null) return false;
        
        PrimaryItemIndex = index;
        HeldItemIndex = index; // 호환성
        return true;
    }

    // 두 번째 일반 슬롯에 아이템 설정
    public bool SetSecondaryItem(int index)
    {
        // 이미 아이템이 있으면 변경 불가
        if (SecondaryItem != null) return false;
        
        SecondaryItemIndex = index;
        return true;
    }

    // 세 번째 슬롯에 아이템 설정 (모든 아이템 가능)
    public bool SetBoosterItem(int index)
    {
        // 이미 아이템이 있으면 변경 불가
        if (BoosterItem != null) return false;
        
        BoosterItemIndex = index;
        return true;
    }

    // 부스터 아이템인지 확인하는 메소드
    private bool IsBoosterItem(int index)
    {
        if (index < 0 || index >= ResourceManager.Instance.powerups.Length)
            return false;
            
        var powerup = ResourceManager.Instance.powerups[index];
        // "Boost"라는 이름이 포함된 아이템을 부스터로 간주
        return powerup.itemName.ToLower().Contains("boost");
    }

    // 빈 슬롯 찾기 (순서대로)
    public int GetEmptySlotIndex()
    {
        if (PrimaryItem == null) return 0;
        if (SecondaryItem == null) return 1;
        if (BoosterItem == null) return 2;
        return -1; // 모든 슬롯이 차있음
    }

    // 모든 슬롯이 차있는지 확인
    public bool AreAllSlotsFull()
    {
        return PrimaryItem != null && SecondaryItem != null && BoosterItem != null;
    }

    // 빈 슬롯이 있는지 확인
    public bool HasEmptySlot()
    {
        return PrimaryItem == null || SecondaryItem == null || BoosterItem == null;
    }

    public void SpinOut()
    {
        Controller.IsSpinout = true;
        StartCoroutine(OnSpinOut());
    }

    private IEnumerator OnSpinOut()
    {
        yield return new WaitForSeconds(2f);
        Controller.IsSpinout = false;
    }
}