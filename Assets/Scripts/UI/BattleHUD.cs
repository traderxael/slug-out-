using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SlugOut.Battle;
using SlugOut.Core;
using SlugOut.Slugs;

namespace SlugOut.UI
{
    public class BattleHUD : MonoBehaviour
    {
        [Header("Barras de Salud")]
        [SerializeField] private Slider playerHealthSlider;
        [SerializeField] private Image playerHealthFill;
        [SerializeField] private Text playerHealthText;

        [SerializeField] private Slider enemyHealthSlider;
        [SerializeField] private Image enemyHealthFill;
        [SerializeField] private Text enemyHealthText;

        [Header("Ranuras de Babosas (Arsenal del Jugador)")]
        [SerializeField] private SlugSlotUI[] slugSlots = new SlugSlotUI[4];

        [Header("Velocimetro Digital")]
        [SerializeField] private Text speedometerText;
        [SerializeField] private GameObject velocimorphAlertBanner;

        [Header("Duelistas")]
        [SerializeField] private Duelist playerDuelist;
        [SerializeField] private Duelist enemyDuelist;

        [Header("Pantalla Final")]
        [SerializeField] private GameObject endBattlePanel;
        [SerializeField] private Text endBattleTitle;
        [SerializeField] private Button restartButton;

        private void Start()
        {
            if (playerDuelist != null)
            {
                playerDuelist.OnHealthChanged += UpdatePlayerHealth;
                playerDuelist.OnSlugEnergyChanged += UpdateSlugSlotEnergy;
                playerDuelist.OnDefeated += () => ShowEndBattle(false);
            }

            if (enemyDuelist != null)
            {
                enemyDuelist.OnHealthChanged += UpdateEnemyHealth;
                enemyDuelist.OnDefeated += () => ShowEndBattle(true);
            }

            SetupSlugSlots();

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (endBattlePanel != null)
            {
                endBattlePanel.SetActive(false);
            }

            if (velocimorphAlertBanner != null)
            {
                velocimorphAlertBanner.SetActive(false);
            }
        }

        private void SetupSlugSlots()
        {
            if (playerDuelist == null) return;

            for (int i = 0; i < slugSlots.Length; i++)
            {
                if (slugSlots[i] == null) continue;

                int slotIndex = i;
                SlugData slug = playerDuelist.GetSlug(i);
                slugSlots[i].Setup(slug, () =>
                {
                    playerDuelist.FireSlug(slotIndex);
                });
            }
        }

        private void UpdatePlayerHealth(float current, float max)
        {
            if (playerHealthSlider != null)
            {
                playerHealthSlider.maxValue = max;
                playerHealthSlider.value = current;
            }
            if (playerHealthText != null)
            {
                playerHealthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        private void UpdateEnemyHealth(float current, float max)
        {
            if (enemyHealthSlider != null)
            {
                enemyHealthSlider.maxValue = max;
                enemyHealthSlider.value = current;
            }
            if (enemyHealthText != null)
            {
                enemyHealthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        private void UpdateSlugSlotEnergy(int slot, float current, float required)
        {
            if (slot >= 0 && slot < slugSlots.Length && slugSlots[slot] != null)
            {
                slugSlots[slot].SetEnergy(current, required);
            }
        }

        public void DisplaySpeed(float mph)
        {
            if (speedometerText != null)
            {
                speedometerText.text = $"{Mathf.RoundToInt(mph)} MPH";
                if (mph >= 100f)
                {
                    speedometerText.color = Color.yellow;
                }
                else
                {
                    speedometerText.color = Color.cyan;
                }
            }

            if (velocimorphAlertBanner != null)
            {
                velocimorphAlertBanner.SetActive(mph >= 100f);
            }
        }

        private void ShowEndBattle(bool victory)
        {
            if (endBattlePanel != null)
            {
                endBattlePanel.SetActive(true);
            }

            if (endBattleTitle != null)
            {
                endBattleTitle.text = victory ? "VICTORIA EN LAS CAVERNAS!" : "DERROTA! EL DR. BLAKK DOMINA";
                endBattleTitle.color = victory ? Color.green : Color.red;
            }
        }

        private void OnRestartClicked()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }
    }

    [System.Serializable]
    public class SlugSlotUI
    {
        public Image slugIcon;
        public Image energyFillBar;
        public GameObject readyGlow;
        public Button fireButton;

        public void Setup(SlugData slug, Action onFire)
        {
            if (slug != null)
            {
                if (slugIcon != null)
                {
                    slugIcon.sprite = slug.protoformSprite;
                    slugIcon.enabled = slug.protoformSprite != null;
                }
            }

            if (fireButton != null)
            {
                fireButton.onClick.RemoveAllListeners();
                fireButton.onClick.AddListener(() => onFire?.Invoke());
                fireButton.interactable = false;
            }

            if (readyGlow != null)
            {
                readyGlow.SetActive(false);
            }
        }

        public void SetEnergy(float current, float required)
        {
            float fill = required > 0f ? Mathf.Clamp01(current / required) : 0f;
            if (energyFillBar != null)
            {
                energyFillBar.fillAmount = fill;
            }

            bool isReady = fill >= 1f;
            if (readyGlow != null)
            {
                readyGlow.SetActive(isReady);
            }

            if (fireButton != null)
            {
                fireButton.interactable = isReady;
            }
        }
    }
}
