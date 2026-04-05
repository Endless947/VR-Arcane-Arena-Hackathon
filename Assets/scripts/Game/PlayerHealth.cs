using UnityEngine;

namespace VRArcaneArena.Game
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance;

        public float maxHealth = 100f;
        public float currentHealth;

        public float HealthPercent => currentHealth / maxHealth;

        private void Awake()
        {
            Instance = this;
            currentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            currentHealth -= amount;
            currentHealth = Mathf.Max(0f, currentHealth);
            Debug.Log($"Player health: {currentHealth}");

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            currentHealth += amount;
            currentHealth = Mathf.Min(maxHealth, currentHealth);
        }

        private void Die()
        {
            Debug.Log("Player died");
            GameManager.Instance.OnPlayerDied();
        }
    }
}
