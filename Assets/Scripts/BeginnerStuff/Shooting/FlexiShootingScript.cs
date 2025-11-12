using UnityEngine;
using Unity.Netcode;
using System.Collections;
using TMPro;

public class FlexiShootingScript : NetworkBehaviour
{
    [Header("References")]
    public StarterAssetsInputs input;
    public Camera playerCamera;
    private MultiplayerFirstPersonController controller;

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
    public int ammoLoaded = 30;
    public int magSize = 30;
    public int reserveAmmo = 90;
    public float reloadTime = 2.0f;
    public float fireRate = 0.1f;
    private float nextFireTime = 0f;

    [Header("Bloom Settings")]
    public float maxBloomAngle = 5f;
    public float bloomIncreaseRate = 1f;
    public float bloomDecreaseRate = 2f;
    private float currentBloom = 0f;

    [Header("Recoil Settings")]
    public float verticalRecoilAmount = 1.5f;
    public float horizontalRecoilAmount = 0.6f;

    [Header("FOV Kick Settings")]
    public float normalFOV = 90f;
    public float recoilFOV = 95f;
    public float fovChangeSpeed = 8f;
    [Tooltip("Time in seconds the FOV stays at recoilFOV before returning to normal.")]
    public float fovKickDuration = 0.2f;

    [Header("Low Ammo Settings")]
    public int lowAmmoThreshold = 5;

    private float targetRecoilX = 0f;
    private float targetRecoilY = 0f;

    private float targetFOV;
    private float fovTimer;

    private void Awake()
    {
        if (!input) input = GetComponent<StarterAssetsInputs>();
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
        controller = GetComponent<MultiplayerFirstPersonController>();

        if (playerCamera)
        {
            playerCamera.fieldOfView = normalFOV;
            targetFOV = normalFOV;
        }

        reloading = false;
        canShoot = true;
        needsReload = false;

        if (ammoLoaded > magSize)
            ammoLoaded = magSize;
        if (ammoLoaded <= 0 && reserveAmmo > 0)
            needsReload = true;

        // Hide UI initially
        if (needsReloadText) needsReloadText.gameObject.SetActive(false);
        if (reloadingText) reloadingText.gameObject.SetActive(false);
        if (lowAmmoText) lowAmmoText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (useNetworking && !IsOwner) return;
        if (!isEquipped) return;

        HandleShooting();
        HandleReloading();
        HandleBloom();
        HandleRecoil();
        HandleFOVKick();
        HandleUI();
    }

    private void HandleShooting()
    {
        if (!canShoot || reloading) return;

        if (input.shoot && Time.time >= nextFireTime)
        {
            if (ammoLoaded > 0)
                Shoot();
            else
                needsReload = true;
        }
    }

    private void Shoot()
    {
        nextFireTime = Time.time + fireRate;
        ammoLoaded--;

        Vector3 shootDir = ApplyBloom(playerCamera.transform.forward);
        Debug.DrawRay(playerCamera.transform.position, shootDir * 100f, Color.red, 0.12f);

        if (Physics.Raycast(playerCamera.transform.position, shootDir, out RaycastHit hit, 200f))
        {
            Health health = hit.collider.GetComponent<Health>();
            if (health != null)
            {
                if (useNetworking)
                {
                    if (IsServer)
                        health.TakeDamage(10f);
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

        currentBloom = Mathf.Min(currentBloom + bloomIncreaseRate, maxBloomAngle);

        // Trigger FOV kick on bullet fire
        targetFOV = recoilFOV;
        fovTimer = fovKickDuration;

        if (ammoLoaded <= 0)
            needsReload = true;
    }

    private void HandleReloading()
    {
        if (reloading) return;
        if (input.reload && ammoLoaded < magSize && reserveAmmo > 0)
            StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        reloading = true;
        canShoot = false;
        UpdateUI(); // Show "Reloading..."

        yield return new WaitForSeconds(reloadTime);

        reserveAmmo += ammoLoaded;
        ammoLoaded = 0;
        int ammoToLoad = Mathf.Min(magSize, reserveAmmo);
        ammoLoaded = ammoToLoad;
        reserveAmmo -= ammoToLoad;

        reloading = false;
        canShoot = true;
        needsReload = false;
        UpdateUI(); // Hide "Reloading..."
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
        if (!input.shoot)
            currentBloom = Mathf.MoveTowards(currentBloom, 0f, bloomDecreaseRate * Time.deltaTime);
    }

    private void HandleRecoil()
    {
        if (controller == null) return;
        if (input.shoot)
            controller.ApplyRecoilInstant(targetRecoilX, targetRecoilY);

        targetRecoilX = Mathf.MoveTowards(targetRecoilX, 0f, 10f * Time.deltaTime);
        targetRecoilY = Mathf.MoveTowards(targetRecoilY, 0f, 10f * Time.deltaTime);
    }

    private void HandleFOVKick()
    {
        if (!playerCamera) return;

        // Apply targetFOV
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, fovChangeSpeed * Time.deltaTime);

        // Countdown timer
        if (fovTimer > 0f)
        {
            fovTimer -= Time.deltaTime;
            if (fovTimer <= 0f)
            {
                targetFOV = normalFOV;
            }
        }
    }

    private void HandleUI()
    {
        UpdateUI();
    }

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
}
