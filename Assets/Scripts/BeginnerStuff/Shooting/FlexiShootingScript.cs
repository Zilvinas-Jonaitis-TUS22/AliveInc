using UnityEngine;
using Unity.Netcode;
using System.Collections;
using TMPro;

public class FlexiShootingScript : NetworkBehaviour
{
    [Header("References")]
    public StarterAssetsInputs input;
    private MultiplayerFirstPersonController controller;
    public Animator weaponAnimator;

    [Header("Cameras")]
    public Camera mainCamera;
    public Camera firstPersonCamera;

    [Header("Owner / Visibility Objects")]
    public GameObject firstPersonObjects;
    public GameObject thirdPersonObjects;

    [Header("Raycast Settings")]
    public Transform raycastOrigin;

    [Header("UI References")]
    public TMP_Text needsReloadText;
    public TMP_Text reloadingText;
    public TMP_Text lowAmmoText;

    [Header("Network Settings")]
    public bool useNetworking = true;

    [Header("Gun Definition")]
    [SerializeField] public WeaponManager weaponManager;

    [Tooltip("Duration the recoil FOV kick lasts (seconds). Editable per-weapon instance.")]
    public float fovKickDuration = 0.15f;
    [Tooltip("How quickly FOV lerps toward targets.")]
    public float fovChangeSpeed = 8f;

    // Internal state
    public int MaxAmmoReserve;
    public int currentAmmoReserve;
    private bool isADS;
    private bool reloading = false;
    private bool canShoot = true;
    private bool needsReload = false;
    private bool hasReleasedSinceLastShot = true;
    private int ammoLoaded = 999;
    private float nextFireTime = 0f;
    private float targetRecoilX = 0f;
    private float targetRecoilY = 0f;
    private float fovKickAmountMain = 0f;
    private float fovKickAmountFP = 0f;
    private float currentBloom;
    private float fovTimer;
    private bool initialized = false;

    public GunDefinitionSO currentGunDefinition = null;

    private void Awake()
    {
        if (!input) input = GetComponent<StarterAssetsInputs>();
        if (!mainCamera) mainCamera = Camera.main;
        if (!firstPersonCamera) firstPersonCamera = GetComponentInChildren<Camera>();
        controller = GetComponent<MultiplayerFirstPersonController>();

        SetGunDefinition(weaponManager.weaponDefinitions[weaponManager.startingWeaponIndex]);

        ammoLoaded = currentGunDefinition.magSize;
        currentBloom = currentGunDefinition.maxBloomAngle;

        HideAllUI();
    }

    public override void OnNetworkSpawn()
    {
        bool isOwnerObject = !useNetworking || IsOwner;

        if (mainCamera)
        {
            mainCamera.enabled = isOwnerObject;
            AudioListener listener = mainCamera.GetComponent<AudioListener>();
            if (listener) listener.enabled = isOwnerObject;
        }

        if (firstPersonCamera)
        {
            firstPersonCamera.enabled = isOwnerObject;
            AudioListener fpListener = firstPersonCamera.GetComponent<AudioListener>();
            if (fpListener) fpListener.enabled = false;
        }

        if (firstPersonObjects) firstPersonObjects.SetActive(isOwnerObject);
        if (thirdPersonObjects) thirdPersonObjects.SetActive(!isOwnerObject);
    }

    private void Start() => StartCoroutine(InitializeAfterFrame());

    private IEnumerator InitializeAfterFrame()
    {
        yield return null;
        if (mainCamera) mainCamera.fieldOfView = currentGunDefinition.mainDefaultFOV;
        if (firstPersonCamera) firstPersonCamera.fieldOfView = currentGunDefinition.fpDefaultFOV;
        initialized = true;
    }

    private void Update()
    {
        if (useNetworking && !IsOwner) return;
        if (!initialized) return;

        if (ammoLoaded <= 0)
        {
            isADS = false;
            weaponAnimator?.SetBool("ADS", false);
        }

        HandleShooting();
        HandleReloading();
        HandleBloom();
        HandleRecoil();
        HandleCameraFOV();
        HandleUI();
        HandleAnimatorADS();
    }

    private void HandleAnimatorADS()
    {
        if (!input || !weaponAnimator) return;

        if (reloading || ammoLoaded <= 0)
        {
            isADS = false;
            weaponAnimator.SetBool("ADS", false);
            return;
        }

        isADS = input.ads;
        weaponAnimator.SetBool("ADS", isADS);
    }

    private void HandleShooting()
    {
        if (reloading) { weaponAnimator?.SetBool("Shooting", false); return; }
        if (!canShoot) return;

        if (currentGunDefinition.fullAuto)
        {
            if (input.shoot && Time.time >= nextFireTime)
            {
                if (ammoLoaded > 0) Shoot();
                else needsReload = true;
            }
        }
        else
        {
            if (input.shoot && hasReleasedSinceLastShot && Time.time >= nextFireTime)
            {
                if (ammoLoaded > 0) Shoot();
                else needsReload = true;

                hasReleasedSinceLastShot = false;
            }
            if (!input.shoot) hasReleasedSinceLastShot = true;
        }
    }

    private void Shoot()
    {
        nextFireTime = Time.time + currentGunDefinition.fireRate;
        ammoLoaded--;

        // --- Raycast ---
        Vector3 origin = raycastOrigin ? raycastOrigin.position : firstPersonCamera.transform.position;
        Vector3 direction = raycastOrigin ? raycastOrigin.forward : firstPersonCamera.transform.forward;
        Vector3 shootDir = ApplyBloom(direction);

        // DEBUG: visualize the shot
        Debug.DrawRay(origin, shootDir * 100f, Color.red, 0.2f);

        if (Physics.Raycast(origin, shootDir, out RaycastHit hit, 200f))
        {
            Health health = hit.collider.GetComponent<Health>();
            if (health != null)
            {
                if (useNetworking && IsServer)
                    health.TakeDamage(currentGunDefinition.damage);
                else if (!useNetworking)
                    health.TakeDamage(currentGunDefinition.damage);
            }
        }

        // --- Recoil ---
        targetRecoilX += currentGunDefinition.verticalRecoilAmount;

        // Horizontal recoil with weight
        float min = -currentGunDefinition.horizontalRecoilAmount;
        float max = currentGunDefinition.horizontalRecoilAmount;
        float weight = Mathf.Clamp(currentGunDefinition.horizontalRecoilWeight, -1f, 1f);

        if (weight < 0f) max *= (1f + weight);      // reduce right movement
        else if (weight > 0f) min *= (1f - weight); // reduce left movement

        targetRecoilY += Random.Range(min, max);

        // Clamp recoil
        targetRecoilX = Mathf.Clamp(targetRecoilX, -30f, 30f);
        targetRecoilY = Mathf.Clamp(targetRecoilY, -30f, 30f);

        // --- Bloom ---
        if (!isADS)
            currentBloom = Mathf.Min(currentBloom + currentGunDefinition.bloomIncreaseRate, currentGunDefinition.maxBloomAngle);

        // --- Animation ---
        weaponAnimator?.SetTrigger("Shoot");

        // --- FOV Recoil ---
        fovKickAmountMain = currentGunDefinition.fovRecoilMain;
        fovKickAmountFP = currentGunDefinition.fovRecoilFP;
        fovTimer = currentGunDefinition.fovKickDuration;

        // --- Ammo check ---
        if (ammoLoaded <= 0)
        {
            needsReload = true;
            isADS = false;
            weaponAnimator?.SetBool("ADS", false);
        }
    }




    private void HandleReloading()
    {
        if (reloading || !input.reload) return;
        if (input.reload && !isADS && (ammoLoaded < currentGunDefinition.magSize) && currentAmmoReserve > 0)
            if (currentAmmoReserve > MaxAmmoReserve)
            {
                currentAmmoReserve = MaxAmmoReserve;
            }
            StartCoroutine(ReloadRoutine());
    }


    private IEnumerator ReloadRoutine()
    {
        reloading = true;
        canShoot = false;

        weaponAnimator?.SetBool("Shooting", false);
        weaponAnimator?.SetBool("Reloading", true);
        weaponAnimator?.SetBool("ADS", false);

        UpdateUI();
        yield return new WaitForSeconds(currentGunDefinition.reloadTime);

        weaponAnimator?.SetBool("Reloading", false);

        currentAmmoReserve += ammoLoaded;
        ammoLoaded = 0;
        int ammoToLoad = Mathf.Min(currentGunDefinition.magSize, currentAmmoReserve);
        ammoLoaded = ammoToLoad;
        currentAmmoReserve -= ammoToLoad;

        reloading = false;
        canShoot = true;
        needsReload = false;

        UpdateUI();
    }


    private Vector3 ApplyBloom(Vector3 direction)
    {
        if (currentBloom <= 0f) return direction;
        float angleX = Random.Range(-currentBloom, currentBloom);
        float angleY = Random.Range(-currentBloom, currentBloom);
        return Quaternion.Euler(angleX, angleY, 0) * direction;
    }

    private void HandleBloom()
    {
        float targetBloom = isADS ? 0f : currentGunDefinition.maxBloomAngle;
        currentBloom = Mathf.Lerp(currentBloom, targetBloom, currentGunDefinition.bloomDecreaseRate * Time.deltaTime);
    }

    private void HandleRecoil()
    {
        if (controller == null) return;
        if (input.shoot) controller.ApplyRecoilInstant(targetRecoilX, targetRecoilY);
        targetRecoilX = Mathf.MoveTowards(targetRecoilX, 0f, 10f * Time.deltaTime);
        targetRecoilY = Mathf.MoveTowards(targetRecoilY, 0f, 10f * Time.deltaTime);
    }

    private void HandleCameraFOV()
    {
        if (!mainCamera || !firstPersonCamera || currentGunDefinition == null) return;

        float baseMain = isADS ? currentGunDefinition.mainADSFOV : currentGunDefinition.mainDefaultFOV;
        float baseFP = isADS ? currentGunDefinition.fpADSFOV : currentGunDefinition.fpDefaultFOV;

        float addMain = (fovTimer > 0f) ? fovKickAmountMain : 0f;
        float addFP = (fovTimer > 0f) ? fovKickAmountFP : 0f;

        float targetMain = baseMain + addMain;
        float targetFP = baseFP + addFP;

        float speed = Mathf.Max(0.001f, currentGunDefinition.fovChangeSpeed);
        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetMain, Time.deltaTime * speed);
        firstPersonCamera.fieldOfView = Mathf.Lerp(firstPersonCamera.fieldOfView, targetFP, Time.deltaTime * speed);

        if (fovTimer > 0f)
            fovTimer -= Time.deltaTime;
    }


    private void HandleUI() => UpdateUI();

    private void UpdateUI()
    {
        if (needsReload && !reloading)
        {
            SetTMPVisibility(needsReloadText, true);
            SetTMPVisibility(reloadingText, false);
        }
        else if (reloading)
        {
            SetTMPVisibility(needsReloadText, false);
            SetTMPVisibility(reloadingText, true);
        }
        else
        {
            SetTMPVisibility(needsReloadText, false);
            SetTMPVisibility(reloadingText, false);
        }

        bool lowAmmo = ammoLoaded > 0 && ammoLoaded <= currentGunDefinition.lowAmmoThreshold && !reloading;
        SetTMPVisibility(lowAmmoText, lowAmmo);
    }

    private void SetTMPVisibility(TMP_Text text, bool visible)
    {
        if (text != null && text.gameObject.activeSelf != visible)
            text.gameObject.SetActive(visible);
    }

    private void HideAllUI()
    {
        if (needsReloadText) needsReloadText.gameObject.SetActive(false);
        if (reloadingText) reloadingText.gameObject.SetActive(false);
        if (lowAmmoText) lowAmmoText.gameObject.SetActive(false);
    }

    public void SetGunDefinition(GunDefinitionSO newDef)
    {
        currentGunDefinition = newDef;
        currentGunDefinition = newDef;
        ammoLoaded = currentGunDefinition.magSize;
        currentBloom = currentGunDefinition.maxBloomAngle;
        MaxAmmoReserve = currentGunDefinition.magSize * 8;
    }
}
