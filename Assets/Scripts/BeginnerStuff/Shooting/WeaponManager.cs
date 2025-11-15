using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    [Header("Weapon Definitions")]
    public GunDefinitionSO[] weaponDefinitions; // Array of GunDefinitionSO
    public int startingWeaponIndex = 0;

    private FlexiShootingScript playerGun; // Reference to the player's FlexiShootingScript

    private void Awake()
    {
        playerGun = GetComponentInChildren<FlexiShootingScript>();
        if (playerGun == null)
        {
            Debug.LogError("No FlexiShootingScript found on player!");
        }
    }

    private void Start()
    {
        EquipWeapon(startingWeaponIndex);
    }

    /// <summary>
    /// Equip a weapon by index in the array
    /// </summary>
    public void EquipWeapon(int index)
    {
        if (index < 0 || index >= weaponDefinitions.Length)
        {
            Debug.LogWarning("Invalid weapon index.");
            return;
        }

        if (playerGun != null)
        {
            playerGun.SetGunDefinition(weaponDefinitions[index]);
        }
    }

    /// <summary>
    /// Equip the next weapon in the array
    /// </summary>
    public void EquipNextWeapon()
    {
        if (weaponDefinitions.Length == 0) return;

        int currentIndex = 0;

        if (playerGun != null && playerGun.currentGunDefinition != null)
            currentIndex = System.Array.IndexOf(weaponDefinitions, playerGun.currentGunDefinition);

        int nextIndex = (currentIndex + 1) % weaponDefinitions.Length;
        EquipWeapon(nextIndex);
    }
}
