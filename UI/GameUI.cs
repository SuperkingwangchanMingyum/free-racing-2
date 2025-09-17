using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class GameUI : MonoBehaviour
{
	public interface IGameUIComponent
	{
		void Init(KartEntity entity);
	}

	public CanvasGroup fader;
	public Animator introAnimator;
	public Animator countdownAnimator;
	
	[Header("아이템 슬롯 1 (Q키/터치)")]
	public Animator itemAnimator;
	public GameObject pickupContainer;
	public Image pickupDisplay;
	public Button pickupButton;
	
	[Header("아이템 슬롯 2 (E키/터치)")]
	public GameObject secondaryPickupContainer;
	public Image secondaryPickupDisplay;
	public Button secondaryPickupButton;
	public Animator secondaryItemAnimator;
	
	[Header("아이템 슬롯 3 (R키/터치)")]
	public GameObject tertiaryPickupContainer;
	public Image tertiaryPickupDisplay;
	public Button tertiaryPickupButton;
	public Animator tertiaryItemAnimator;
	
	[Header("빈 슬롯 아이콘")]
	public Sprite emptySlotIcon; // ? 아이콘
	
	[Header("기타 UI")]
	public GameObject timesContainer;
	public GameObject coinCountContainer;
	public GameObject lapCountContainer;
	public EndRaceUI endRaceScreen;
	public Image boostBar;
	public Text coinCount;
	public Text lapCount;
	public Text raceTimeText;
	public Text[] lapTimeTexts;
	public Text introGameModeText;
	public Text introTrackNameText;
	public Button continueEndButton;
	
	private bool _startedCountdown;
	
	// 이전 아이템 인덱스 추적용 (클래스 레벨 변수로 이동)
	private int previousPrimaryIndex = -1;
	private int previousSecondaryIndex = -1;
	private int previousTertiaryIndex = -1;

	public KartEntity Kart { get; private set; }
	private KartController KartController => Kart.Controller;

	public void Init(KartEntity kart)
	{
		Kart = kart;

		var uis = GetComponentsInChildren<IGameUIComponent>(true);
		foreach (var ui in uis) ui.Init(kart);

		kart.LapController.OnLapChanged += SetLapCount;

		var track = Track.Current;

		if (track == null)
			Debug.LogWarning($"You need to initialize the GameUI on a track for track-specific values to be updated!");
		else
		{
			introGameModeText.text = GameManager.Instance.GameType.modeName;
			introTrackNameText.text = track.definition.trackName;
		}

		GameType gameType = GameManager.Instance.GameType;

		if (gameType.IsPracticeMode())
		{
			timesContainer.SetActive(false);
			lapCountContainer.SetActive(false);
		}

		if (gameType.hasPickups == false)
		{
			if (pickupContainer) pickupContainer.SetActive(false);
			if (secondaryPickupContainer) secondaryPickupContainer.SetActive(false);
			if (tertiaryPickupContainer) tertiaryPickupContainer.SetActive(false);
		}
		else
		{
			// 모든 슬롯 초기화
			ClearPickupDisplay();
			ClearSecondaryDisplay();
			ClearTertiaryDisplay();
			
			// 버튼 설정
			SetupItemButtons();
		}

		if (gameType.hasCoins == false)
		{
			coinCountContainer.SetActive(false);
		}

		continueEndButton.gameObject.SetActive(kart.Object.HasStateAuthority);

		// 3슬롯 이벤트 연결
		SetupItemEvents();

		kart.OnCoinCountChanged += count =>
		{
			AudioManager.Play("coinSFX", AudioManager.MixerTarget.SFX);
			coinCount.text = $"{count:00}";
		};
	}
	
	private void SetupItemEvents()
	{
		// Primary 슬롯 (슬롯 1) - OnPrimaryItemChanged만 사용
		Kart.OnPrimaryItemChanged += index =>
		{
			// 다른 슬롯으로 아이템이 이동한 경우 무시
			if (previousPrimaryIndex != -1 && index == previousPrimaryIndex)
			{
				// 아이템이 변경되지 않았으면 무시
				return;
			}
			
			if (index == -1)
			{
				ClearPickupDisplay();
				previousPrimaryIndex = -1;
			}
			else
			{
				// 이전에 비어있었을 때만 스핀
				if (previousPrimaryIndex == -1)
				{
					if (itemAnimator != null)
					{
						StartSpinItem();
					}
					else
					{
						StartCoroutine(FakeSpinRoutine(pickupDisplay, index));
					}
				}
				else if (previousPrimaryIndex != index)
				{
					// 아이템이 변경된 경우에만 업데이트
					SetPickupDisplay(ResourceManager.Instance.powerups[index]);
				}
				previousPrimaryIndex = index;
			}
			UpdateButtonState(pickupButton, index != -1);
		};
		
		// OnHeldItemChanged는 호환성을 위해 유지하되, Primary와 동기화만
		Kart.OnHeldItemChanged += index =>
		{
			// Primary와 동기화되므로 별도 처리 불필요
			// 이미 OnPrimaryItemChanged에서 처리됨
		};
		
		// 슬롯 2 (Secondary)
		Kart.OnSecondaryItemChanged += index =>
		{
			// 다른 슬롯으로 아이템이 이동한 경우 무시
			if (previousSecondaryIndex != -1 && index == previousSecondaryIndex)
			{
				return;
			}
			
			if (index == -1)
			{
				ClearSecondaryDisplay();
				previousSecondaryIndex = -1;
			}
			else
			{
				// 이전에 비어있었을 때만 스핀
				if (previousSecondaryIndex == -1)
				{
					if (secondaryItemAnimator != null)
					{
						StartSecondarySpinItem();
					}
					else
					{
						StartCoroutine(FakeSpinRoutine(secondaryPickupDisplay, index));
					}
				}
				else if (previousSecondaryIndex != index)
				{
					// 아이템이 변경된 경우에만 업데이트
					SetSecondaryDisplay(ResourceManager.Instance.powerups[index]);
				}
				previousSecondaryIndex = index;
			}
			UpdateButtonState(secondaryPickupButton, index != -1);
		};
		
		// 슬롯 3 (Tertiary - BoosterItem을 일반 슬롯으로 사용)
		Kart.OnBoosterItemChanged += index =>
		{
			// 다른 슬롯으로 아이템이 이동한 경우 무시
			if (previousTertiaryIndex != -1 && index == previousTertiaryIndex)
			{
				return;
			}
			
			if (index == -1)
			{
				ClearTertiaryDisplay();
				previousTertiaryIndex = -1;
			}
			else
			{
				// 이전에 비어있었을 때만 스핀
				if (previousTertiaryIndex == -1)
				{
					if (tertiaryItemAnimator != null)
					{
						StartTertiarySpinItem();
					}
					else
					{
						StartCoroutine(FakeSpinRoutine(tertiaryPickupDisplay, index));
					}
				}
				else if (previousTertiaryIndex != index)
				{
					// 아이템이 변경된 경우에만 업데이트
					SetTertiaryDisplay(ResourceManager.Instance.powerups[index]);
				}
				previousTertiaryIndex = index;
			}
			UpdateButtonState(tertiaryPickupButton, index != -1);
		};
	}
	
	private void SetupItemButtons()
	{
		// 슬롯 1 버튼 - 터치 시 순차 사용
		if (pickupButton != null)
		{
			pickupButton.onClick.RemoveAllListeners();
			pickupButton.onClick.AddListener(() =>
			{
				if (Kart && Kart.Items)
				{
					// 순차적으로 사용 (Shift키와 동일)
					Kart.Items.UseNextAvailableItem();
					AnimateButtonPress(pickupButton.transform);
					AudioManager.Play("useItemSFX", AudioManager.MixerTarget.SFX);
				}
			});
		}
		
		// 슬롯 2 버튼 - 직접 사용
		if (secondaryPickupButton != null)
		{
			secondaryPickupButton.onClick.RemoveAllListeners();
			secondaryPickupButton.onClick.AddListener(() =>
			{
				if (Kart && Kart.Items && Kart.SecondaryItem != null)
				{
					Kart.Items.UseSecondaryItem();
					AnimateButtonPress(secondaryPickupButton.transform);
					AudioManager.Play("useItemSFX", AudioManager.MixerTarget.SFX);
				}
			});
		}
		
		// 슬롯 3 버튼 - 직접 사용
		if (tertiaryPickupButton != null)
		{
			tertiaryPickupButton.onClick.RemoveAllListeners();
			tertiaryPickupButton.onClick.AddListener(() =>
			{
				if (Kart && Kart.Items && Kart.BoosterItem != null)
				{
					Kart.Items.UseBoosterItem();
					AnimateButtonPress(tertiaryPickupButton.transform);
					AudioManager.Play("useItemSFX", AudioManager.MixerTarget.SFX);
				}
			});
		}
	}
	
	private void UpdateButtonState(Button button, bool hasItem)
	{
		if (button != null)
		{
			button.interactable = hasItem;
			// 시각적 피드백
			var image = button.GetComponent<Image>();
			if (image != null)
			{
				image.color = hasItem ? new Color(1, 1, 1, 1f) : new Color(1, 1, 1, 0.3f);
			}
		}
	}
	
	private void AnimateButtonPress(Transform buttonTransform)
	{
		StartCoroutine(ButtonPressAnimation(buttonTransform));
	}
	
	private IEnumerator ButtonPressAnimation(Transform target)
	{
		if (target == null) yield break;
		
		Vector3 originalScale = target.localScale;
		float duration = 0.15f;
		float elapsed = 0;
		
		// 축소
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / duration;
			target.localScale = Vector3.Lerp(originalScale, originalScale * 0.85f, t);
			yield return null;
		}
		
		// 복원
		elapsed = 0;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / duration;
			target.localScale = Vector3.Lerp(originalScale * 0.85f, originalScale, t);
			yield return null;
		}
		
		target.localScale = originalScale;
	}

	private void OnDestroy()
	{
		if (Kart != null && Kart.LapController != null)
			Kart.LapController.OnLapChanged -= SetLapCount;
	}
	
	public void FinishCountdown()
	{
		// Kart.OnRaceStart();
	}

	public void HideIntro()
	{
		introAnimator.SetTrigger("Exit");
	}

	private void FadeIn()
	{
		StartCoroutine(FadeInRoutine());
	}

	private IEnumerator FadeInRoutine()
	{
		float t = 1;
		while (t > 0)
		{
			fader.alpha = 1 - t;
			t -= Time.deltaTime;
			yield return null;
		}
	}

	private void Update()
	{
		if (!Kart || !Kart.LapController.Object || !Kart.LapController.Object.IsValid)
			return;

		if (!_startedCountdown && Track.Current != null && Track.Current.StartRaceTimer.IsRunning)
		{
			var remainingTime = Track.Current.StartRaceTimer.RemainingTime(Kart.Runner);
			if (remainingTime != null && remainingTime <= 3.0f)
			{
				_startedCountdown = true;
				HideIntro();
				FadeIn();
				countdownAnimator.SetTrigger("StartCountdown");
			}
		}

		UpdateBoostBar();

		if (Kart.LapController.enabled) UpdateLapTimes();

		var controller = Kart.Controller;
		if (controller.BoostTime > 0f)
		{
			if (controller.BoostTierIndex == -1) return;

			Color color = controller.driftTiers[controller.BoostTierIndex].color;
			SetBoostBarColor(color);
		}
		else
		{
			if (!controller.IsDrifting) return;

			SetBoostBarColor(controller.DriftTierIndex < controller.driftTiers.Length - 1
				? controller.driftTiers[controller.DriftTierIndex + 1].color
				: controller.driftTiers[controller.DriftTierIndex].color);
		}
	}

	private void UpdateBoostBar()
	{
		if (!KartController.Object || !KartController.Object.IsValid)
			return;
		
		var driftIndex = KartController.DriftTierIndex;
		var boostIndex = KartController.BoostTierIndex;

		if (KartController.IsDrifting)
		{
			if (driftIndex < KartController.driftTiers.Length - 1)
				SetBoostBar((KartController.DriftTime - KartController.driftTiers[driftIndex].startTime) /
				            (KartController.driftTiers[driftIndex + 1].startTime - KartController.driftTiers[driftIndex].startTime));
			else
				SetBoostBar(1);
		}
		else
		{
			SetBoostBar(boostIndex == -1
				? 0f
				: KartController.BoostTime / KartController.driftTiers[boostIndex].boostDuration);
		}
	}

	private void UpdateLapTimes()
	{
		if (!Kart.LapController.Object || !Kart.LapController.Object.IsValid)
			return;
		var lapTimes = Kart.LapController.LapTicks;
		for (var i = 0; i < Mathf.Min(lapTimes.Length, lapTimeTexts.Length); i++)
		{
			var lapTicks = lapTimes.Get(i);

			if (lapTicks == 0)
			{
				lapTimeTexts[i].text = "";
			}
			else
			{
				var previousTicks = i == 0
					? Kart.LapController.StartRaceTick
					: lapTimes.Get(i - 1);

				var deltaTicks = lapTicks - previousTicks;
				var time = TickHelper.TickToSeconds(Kart.Runner, deltaTicks);

				SetLapTimeText(time, i);
			}
		}

		SetRaceTimeText(Kart.LapController.GetTotalRaceTime());
	}

	public void SetBoostBar(float amount)
	{
		boostBar.fillAmount = amount;
	}

	public void SetBoostBarColor(Color color)
	{
		boostBar.color = color;
	}

	public void SetCoinCount(int count)
	{
		coinCount.text = $"{count:00}";
	}

	private void SetLapCount(int lap, int maxLaps)
	{
		var text = $"{(lap > maxLaps ? maxLaps : lap)}/{maxLaps}";
		lapCount.text = text;
	}

	public void SetRaceTimeText(float time)
	{
		raceTimeText.text = $"{(int) (time / 60):00}:{time % 60:00.000}";
	}

	public void SetLapTimeText(float time, int index)
	{
		lapTimeTexts[index].text = $"<color=#FFC600>L{index + 1}</color> {(int) (time / 60):00}:{time % 60:00.000}";
	}

	// 슬롯 1 메소드들
	public void StartSpinItem()
	{
		StartCoroutine(SpinItemRoutine());
	}

	private IEnumerator SpinItemRoutine()
	{
		if (itemAnimator != null)
		{
			itemAnimator.SetBool("Ticking", true);
			float dur = 3;
			float spd = Random.Range(9f, 11f);
			float x = 0;
			
			// 스핀 중 랜덤 아이템 표시
			while (x < dur - 0.5f)
			{
				x += Time.deltaTime;
				itemAnimator.speed = (spd - 1) / (dur * dur) * (x - dur) * (x - dur) + 1;
				
				// 랜덤 아이템 표시
				if (pickupDisplay != null)
				{
					int randomIndex = Random.Range(0, ResourceManager.Instance.powerups.Length);
					pickupDisplay.sprite = ResourceManager.Instance.powerups[randomIndex].itemIcon;
				}
				
				yield return null;
			}
			
			// 나머지 시간 동안 최종 아이템으로
			while (x < dur)
			{
				x += Time.deltaTime;
				itemAnimator.speed = (spd - 1) / (dur * dur) * (x - dur) * (x - dur) + 1;
				yield return null;
			}

			itemAnimator.SetBool("Ticking", false);
		}
		else
		{
			// 애니메이터가 없으면 가짜 스핀
			yield return StartCoroutine(FakeSpinRoutine(pickupDisplay, Kart.PrimaryItemIndex));
		}
		
		// 최종 아이템 표시 - PrimaryItem을 사용
		if (Kart.PrimaryItemIndex != -1)
			SetPickupDisplay(ResourceManager.Instance.powerups[Kart.PrimaryItemIndex]);
		else
			ClearPickupDisplay();
	}

	public void SetPickupDisplay(Powerup item)
	{
		if (pickupDisplay == null) return;
		
		if (item)
			pickupDisplay.sprite = item.itemIcon;
		else
			pickupDisplay.sprite = emptySlotIcon != null ? emptySlotIcon : null;
	}

	public void ClearPickupDisplay()
	{
		if (ResourceManager.Instance.noPowerup != null)
			SetPickupDisplay(ResourceManager.Instance.noPowerup);
		else if (emptySlotIcon != null && pickupDisplay != null)
			pickupDisplay.sprite = emptySlotIcon;
		
		UpdateButtonState(pickupButton, false);
	}
	
	// 슬롯 2 메소드들
	public void StartSecondarySpinItem()
	{
		if (secondaryItemAnimator != null)
			StartCoroutine(SecondarySpinItemRoutine());
		else
			// 애니메이터가 없으면 가짜 스핀 효과
			StartCoroutine(FakeSpinRoutine(secondaryPickupDisplay, Kart.SecondaryItemIndex));
	}
	
	private IEnumerator SecondarySpinItemRoutine()
	{
		secondaryItemAnimator.SetBool("Ticking", true);
		float dur = 3;
		float spd = Random.Range(9f, 11f);
		float x = 0;
		
		// 스핀 중 랜덤 아이템 표시
		while (x < dur - 0.5f)
		{
			x += Time.deltaTime;
			secondaryItemAnimator.speed = (spd - 1) / (dur * dur) * (x - dur) * (x - dur) + 1;
			
			// 랜덤 아이템 표시
			if (secondaryPickupDisplay != null)
			{
				int randomIndex = Random.Range(0, ResourceManager.Instance.powerups.Length);
				secondaryPickupDisplay.sprite = ResourceManager.Instance.powerups[randomIndex].itemIcon;
			}
			
			yield return null;
		}
		
		// 나머지 시간 동안 최종 아이템으로
		while (x < dur)
		{
			x += Time.deltaTime;
			secondaryItemAnimator.speed = (spd - 1) / (dur * dur) * (x - dur) * (x - dur) + 1;
			yield return null;
		}

		secondaryItemAnimator.SetBool("Ticking", false);
		
		// 최종 아이템 표시 - SecondaryItem을 사용
		if (Kart.SecondaryItemIndex != -1)
			SetSecondaryDisplay(ResourceManager.Instance.powerups[Kart.SecondaryItemIndex]);
		else
			ClearSecondaryDisplay();
	}
	
	public void SetSecondaryDisplay(Powerup item)
	{
		if (secondaryPickupDisplay == null) return;
		
		if (item)
			secondaryPickupDisplay.sprite = item.itemIcon;
		else
			secondaryPickupDisplay.sprite = emptySlotIcon != null ? emptySlotIcon : null;
	}
	
	public void ClearSecondaryDisplay()
	{
		if (secondaryPickupDisplay != null)
		{
			secondaryPickupDisplay.sprite = emptySlotIcon != null ? emptySlotIcon : 
				(ResourceManager.Instance.noPowerup != null ? ResourceManager.Instance.noPowerup.itemIcon : null);
		}
		UpdateButtonState(secondaryPickupButton, false);
	}
	
	// 슬롯 3 메소드들
	public void StartTertiarySpinItem()
	{
		if (tertiaryItemAnimator != null)
			StartCoroutine(TertiarySpinItemRoutine());
		else
			// 애니메이터가 없으면 가짜 스핀 효과
			StartCoroutine(FakeSpinRoutine(tertiaryPickupDisplay, Kart.BoosterItemIndex));
	}
	
	private IEnumerator TertiarySpinItemRoutine()
	{
		tertiaryItemAnimator.SetBool("Ticking", true);
		float dur = 3;
		float spd = Random.Range(9f, 11f);
		float x = 0;
		
		// 스핀 중 랜덤 아이템 표시
		while (x < dur - 0.5f)
		{
			x += Time.deltaTime;
			tertiaryItemAnimator.speed = (spd - 1) / (dur * dur) * (x - dur) * (x - dur) + 1;
			
			// 랜덤 아이템 표시
			if (tertiaryPickupDisplay != null)
			{
				int randomIndex = Random.Range(0, ResourceManager.Instance.powerups.Length);
				tertiaryPickupDisplay.sprite = ResourceManager.Instance.powerups[randomIndex].itemIcon;
			}
			
			yield return null;
		}
		
		// 나머지 시간 동안 최종 아이템으로
		while (x < dur)
		{
			x += Time.deltaTime;
			tertiaryItemAnimator.speed = (spd - 1) / (dur * dur) * (x - dur) * (x - dur) + 1;
			yield return null;
		}

		tertiaryItemAnimator.SetBool("Ticking", false);
		
		// 최종 아이템 표시 - BoosterItem을 사용
		if (Kart.BoosterItemIndex != -1)
			SetTertiaryDisplay(ResourceManager.Instance.powerups[Kart.BoosterItemIndex]);
		else
			ClearTertiaryDisplay();
	}
	
	// 애니메이터가 없을 때 가짜 스핀 효과
	private IEnumerator FakeSpinRoutine(Image display, int finalItemIndex)
	{
		if (display == null || finalItemIndex < 0) yield break;
		
		float dur = 3;
		float x = 0;
		
		// 랜덤 아이템 표시
		while (x < dur - 0.5f)
		{
			x += Time.deltaTime;
			
			// 0.1초마다 랜덤 아이템 변경
			if ((int)(x * 10) % 1 == 0)
			{
				int randomIndex = Random.Range(0, ResourceManager.Instance.powerups.Length);
				display.sprite = ResourceManager.Instance.powerups[randomIndex].itemIcon;
				
				// 사운드 효과
				if (x < 2.5f && (int)(x * 5) % 1 == 0)
				{
					AudioManager.Play("tickItemUI", AudioManager.MixerTarget.UI);
				}
			}
			
			yield return null;
		}
		
		// 최종 아이템 표시
		display.sprite = ResourceManager.Instance.powerups[finalItemIndex].itemIcon;
		AudioManager.Play("itemCollectSFX", AudioManager.MixerTarget.SFX);
	}
	
	public void SetTertiaryDisplay(Powerup item)
	{
		if (tertiaryPickupDisplay == null) return;
		
		if (item)
			tertiaryPickupDisplay.sprite = item.itemIcon;
		else
			tertiaryPickupDisplay.sprite = emptySlotIcon != null ? emptySlotIcon : null;
	}
	
	public void ClearTertiaryDisplay()
	{
		if (tertiaryPickupDisplay != null)
		{
			tertiaryPickupDisplay.sprite = emptySlotIcon != null ? emptySlotIcon : 
				(ResourceManager.Instance.noPowerup != null ? ResourceManager.Instance.noPowerup.itemIcon : null);
		}
		UpdateButtonState(tertiaryPickupButton, false);
	}

	public void ShowEndRaceScreen()
	{
		endRaceScreen.gameObject.SetActive(true);
	}

	public void OpenPauseMenu()
	{
		InterfaceManager.Instance.OpenPauseMenu();
	}
}