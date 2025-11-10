using UnityEngine;
using Unity.Netcode;

public class Health : NetworkBehaviour
{
    [Header("Networking / Testing")]
    [Tooltip("If false, health changes are local and do not use networking.")]
    public bool useNetworking = true;

    [Header("Health Settings")]
    public float maxHealth = 100f;

    // Current health (networked if useNetworking)
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>();

    [Header("Death Settings")]
    public bool destroyOnDeath = false; // Destroy GameObject when health reaches 0

    public delegate void HealthChanged(float current, float max);
    public event HealthChanged OnHealthChanged;

    public delegate void DeathEvent();
    public event DeathEvent OnDeath;

    private float localHealth; // Used if networking is disabled

    private void Start()
    {
        if (useNetworking)
        {
            currentHealth.Value = maxHealth;
        }
        else
        {
            localHealth = maxHealth;
            OnHealthChanged?.Invoke(localHealth, maxHealth);
        }
    }

    /// <summary>
    /// Apply damage to this object.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (useNetworking)
        {
            if (!IsServer) return; // Only server modifies networked health

            currentHealth.Value -= amount;
            currentHealth.Value = Mathf.Max(currentHealth.Value, 0f);

            OnHealthChanged?.Invoke(currentHealth.Value, maxHealth);

            if (currentHealth.Value <= 0f)
                Die();
        }
        else
        {
            localHealth -= amount;
            localHealth = Mathf.Max(localHealth, 0f);

            OnHealthChanged?.Invoke(localHealth, maxHealth);

            if (localHealth <= 0f)
                Die();
        }
    }

    /// <summary>
    /// Heal this object.
    /// </summary>
    public void Heal(float amount)
    {
        if (useNetworking)
        {
            if (!IsServer) return;

            currentHealth.Value += amount;
            currentHealth.Value = Mathf.Min(currentHealth.Value, maxHealth);

            OnHealthChanged?.Invoke(currentHealth.Value, maxHealth);
        }
        else
        {
            localHealth += amount;
            localHealth = Mathf.Min(localHealth, maxHealth);

            OnHealthChanged?.Invoke(localHealth, maxHealth);
        }
    }

    private void Die()
    {
        OnDeath?.Invoke();

        Debug.Log($"{gameObject.name} has died!");

        if (destroyOnDeath)
            Destroy(gameObject);
    }

    /// <summary>
    /// Get the current health value (networked or local).
    /// </summary>
    public float GetCurrentHealth()
    {
        return useNetworking ? currentHealth.Value : localHealth;
    }
}
