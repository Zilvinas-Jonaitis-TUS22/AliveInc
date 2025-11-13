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
    public GameObject firstPersonObjects; // Visible only to owner
    public GameObject thirdPersonObjects; // Visible only to non-owners

    [Header("Main Camera FOV Settings")]
    public float mainDefaultFOV = 90f;
    public float mainADSFOV = 60f;
    public float mainRecoilFOV = 3f;

    [Header("First Person Camera FOV Settings")]
    public float fpDefaultFOV = 80f;
    public float fpADSFOV = 50f;
    public float fpRecoilFOV = 2f;

    [Header("Camera Settings")]
    public float fovChangeSpeed = 8f;
    public float fovKickDuration = 0.2f;

    private float fovTimer;
    private bool initialized = false;

    [Header("Raycast Settings")]
    public Transform raycastOrigin;

    [Header("UI References")]
    public TMP_Text needsReloadText;
    public TMP_Text reloadingText;
    public TMP_Text lowAmmoText;

    [Header("Network Settings")]
    public bool useNetworking = true;

    [Header("Weapon Settings")]
    public bool isEquipped = true;
    public bool canShoot = true;
    public bool needsReload = false;
    public bool reloading = false;
    public bool fullAuto = true;
    public int ammoLoaded = 30;
    public int magSize = 30;
    public int reserveAmmo = 90;
    public float reloadTime = 2.0f;
    public float fireRate = 0.1f;
    private float nextFireTime = 0f;
    private bool hasReleasedSinceLastShot = true;

    [Header("Bloom Settings")]
    public float maxBloomAngle = 5f;
    public float bloomIncreaseRate = 1f;
    public float bloomDecreaseRate = 5f;
    private float currentBloom = 5f;

    [Header("Recoil Settings")]
    public float verticalRecoilAmount = 1.5f;
    public float horizontalRecoilAmount = 0.6f;

    [Header("Low Ammo Settings")]
    public int lowAmmoThreshold = 5;

    private float targetRecoilX = 0f;
    private float targetRecoilY = 0f;
    private bool isADS;

    private void Awake()
    {
        if (!input) input = GetComponent<StarterAssetsInputs>();
        if (!mainCamera) mainCamera = Camera.main;
        if (!firstPersonCamera) firstPersonCamera = GetComponentInChildren<Camera>();
        controller = GetComponent<MultiplayerFirstPersonController>();

        if (mainCamera) mainCamera.fieldOfView = mainDefaultFOV;
        if (firstPersonCamera) firstPersonCamera.fieldOfView = fpDefaultFOV;

        reloading = false;
        canShoot = true;
        needsReload = false;

        ammoLoaded = Mathf.Clamp(ammoLoaded, 0, magSize);
        if (ammoLoaded <= 0 && reserveAmmo > 0) needsReload = true;

        HideAllUI();
    }

    public override void OnNetworkSpawn()
    {
        bool isOwnerObject = !useNetworking || IsOwner;

        // Cameras for owner only
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

        // First-person objects visible only to owner
        if (firstPersonObjects)
            firstPersonObjects.SetActive(isOwnerObject);

        // Third-person objects visible only to others
        if (thirdPersonObjects)
            thirdPersonObjects.SetActive(!isOwnerObject);
    }

    private void Start()
    {
        StartCoroutine(InitializeAfterFrame());
    }

    private IEnumerator InitializeAfterFrame()
    {
        yield return null;
        if (mainCamera) mainCamera.fieldOfView = mainDefaultFOV;
        if (firstPersonCamera) firstPersonCamera.fieldOfView = fpDefaultFOV;
        initialized = true;
    }

    private void Update()
    {
        if (useNetworking && !IsOwner) return;
        if (!isEquipped || !initialized) return;

        if (ammoLoaded <= 0)
        {
            isADS = false;
            if (weaponAnimator) weaponAnimator.SetBool("ADS", false);
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
        if (reloading)
        {
            if (weaponAnimator)
                weaponAnimator.SetBool("Shooting", false);
            return;
        }

        if (!canShoot) return;

        if (fullAuto)
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

            if (!input.shoot)
                hasReleasedSinceLastShot = true;
        }

        if (weaponAnimator)
            weaponAnimator.SetBool("Shooting", input.shoot && canShoot && !reloading && ammoLoaded > 0);
    }

    private void Shoot()
    {
        nextFireTime = Time.time + fireRate;
        ammoLoaded--;

        Vector3 origin = raycastOrigin ? raycastOrigin.position : firstPersonCamera.transform.position;
        Vector3 direction = raycastOrigin ? raycastOrigin.forward : firstPersonCamera.transform.forward;
        Vector3 shootDir = ApplyBloom(direction);

        Debug.DrawRay(origin, shootDir * 100f, Color.red, 0.12f);
        if (Physics.Raycast(origin, shootDir, out RaycastHit hit, 200f))
        {
            Health health = hit.collider.GetComponent<Health>();
            if (health != null)
            {
                if (useNetworking)
                {
                    if (IsServer) health.TakeDamage(10f);
                }
                else
                {
                    health.TakeDamage(10f);
                }
            }
        }

        targetRecoilX += verticalRecoilAmount;
        targetRecoilY += Random.Range(-horizontalRecoilAmount, horizontalRecoilAmount);
        targetRecoilX = Mathf.Clamp(targetRecoilX, -30f, 30f);
        targetRecoilY = Mathf.Clamp(targetRecoilY, -30f, 30f);

        if (!isADS)
            currentBloom = Mathf.Min(currentBloom + bloomIncreaseRate, maxBloomAngle);

        if (weaponAnimator && ammoLoaded > 0)
            weaponAnimator.SetTrigger("Shoot");

        fovTimer = fovKickDuration;

        if (ammoLoaded <= 0)
        {
            needsReload = true;
            isADS = false;
            if (weaponAnimator) weaponAnimator.SetBool("ADS", false);
        }
    }

    private void HandleReloading()
    {
        if (reloading) return;

        if (input.reload && !isADS && ammoLoaded < magSize && reserveAmmo > 0)
            StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        reloading = true;
        canShoot = false;

        if (weaponAnimator)
        {
            weaponAnimator.SetBool("Shooting", false);
            weaponAnimator.SetBool("Reloading", true);
            weaponAnimator.SetBool("ADS", false);
        }

        UpdateUI();
        yield return new WaitForSeconds(reloadTime);

        if (weaponAnimator)
            weaponAnimator.SetBool("Reloading", false);

        reserveAmmo += ammoLoaded;
        ammoLoaded = 0;
        int ammoToLoad = Mathf.Min(magSize, reserveAmmo);
        ammoLoaded = ammoToLoad;
        reserveAmmo -= ammoToLoad;

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
        Quaternion rotation = Quaternion.Euler(angleX, angleY, 0);
        return rotation * direction;
    }

    private void HandleBloom()
    {
        float targetBloom = isADS ? 0f : maxBloomAngle;
        currentBloom = Mathf.Lerp(currentBloom, targetBloom, bloomDecreaseRate * Time.deltaTime);
    }

    private void HandleRecoil()
    {
        if (controller == null) return;

        if (input.shoot)
            controller.ApplyRecoilInstant(targetRecoilX, targetRecoilY);

        targetRecoilX = Mathf.MoveTowards(targetRecoilX, 0f, 10f * Time.deltaTime);
        targetRecoilY = Mathf.MoveTowards(targetRecoilY, 0f, 10f * Time.deltaTime);
    }

    private void HandleCameraFOV()
    {
        if (!mainCamera || !firstPersonCamera) return;

        float mainBaseFOV = isADS ? mainADSFOV : mainDefaultFOV;
        float fpBaseFOV = isADS ? fpADSFOV : fpDefaultFOV;

        float mainTarget = mainBaseFOV + (fovTimer > 0f ? mainRecoilFOV : 0f);
        float fpTarget = fpBaseFOV + (fovTimer > 0f ? fpRecoilFOV : 0f);

        if (fovTimer > 0f)
            fovTimer -= Time.deltaTime;

        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, mainTarget, Time.deltaTime * fovChangeSpeed);
        firstPersonCamera.fieldOfView = Mathf.Lerp(firstPersonCamera.fieldOfView, fpTarget, Time.deltaTime * fovChangeSpeed);
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

        bool lowAmmo = ammoLoaded > 0 && ammoLoaded <= lowAmmoThreshold && !reloading;
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
}
