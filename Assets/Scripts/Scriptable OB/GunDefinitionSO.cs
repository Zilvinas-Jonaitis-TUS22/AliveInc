using UnityEngine;

[CreateAssetMenu(fileName = "NewGunDefinition", menuName = "Weapons/GunDefinition")]
public class GunDefinitionSO : ScriptableObject
{
    [Header("Gun Info")]
    public string gunName = "New Gun";
    public int damage = 10;

    [Header("Ammo Settings")]
    public int magSize = 30;
    public float reloadTime = 2.0f;
    public bool fullAuto = true;
    public int lowAmmoThreshold = 5;

    [Header("Fire Settings")]
    public float fireRate = 0.1f;

    [Header("Recoil Settings")]
    public float verticalRecoilAmount = 1.5f;
    public float horizontalRecoilAmount = 0.6f;
    [Range(-1f, 1f)]
    public float horizontalRecoilWeight = 0f;

    [Header("Bloom Settings")]
    public float maxBloomAngle = 5f;
    public float bloomIncreaseRate = 1f;
    public float bloomDecreaseRate = 5f;

    [Header("Main Camera FOV")]
    public float mainDefaultFOV = 90f;
    public float mainADSFOV = 60f;
    public float mainRecoilFOV = 3f;

    [Header("First Person Camera FOV")]
    public float fpDefaultFOV = 80f;
    public float fpADSFOV = 50f;
    public float fpRecoilFOV = 2f;

    [Header("FOV Recoil Settings")]
    public float fovRecoilMain = 3f;      // How much main camera FOV kicks on recoil
    public float fovRecoilFP = 2f;        // How much first-person FOV kicks on recoil
    public float fovKickDuration = 0.15f; // Duration of the FOV kick effect
    public float fovChangeSpeed = 8f;     // Speed at which FOV lerps back

}
