using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class FlexiShootingScript : NetworkBehaviour
{
    [Header("References")]
    public StarterAssetsInputs input;
    public Camera playerCamera;
    private MultiplayerFirstPersonController controller;

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

    private float targetRecoilX = 0f; // vertical
    private float targetRecoilY = 0f; // horizontal

    private void Awake()
    {
        if (!input) input = GetComponent<StarterAssetsInputs>();
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
        controller = GetComponent<MultiplayerFirstPersonController>();
    }

    private void Update()
    {
        if (useNetworking && !IsOwner) return;
        if (!isEquipped) return;

        HandleShooting();
        HandleReloading();
        HandleBloom();
        HandleRecoil();
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

        // Apply bloom
        Vector3 shootDir = ApplyBloom(playerCamera.transform.forward);

        // Debug ray
        Debug.DrawRay(playerCamera.transform.position, shootDir * 100f, Color.red, 0.12f);

        // Raycast hit detection
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

        // Add recoil
        targetRecoilX += verticalRecoilAmount;
        targetRecoilY += Random.Range(-horizontalRecoilAmount, horizontalRecoilAmount);

        // Clamp to reasonable values
        targetRecoilX = Mathf.Clamp(targetRecoilX, -30f, 30f);
        targetRecoilY = Mathf.Clamp(targetRecoilY, -30f, 30f);

        // Increase bloom
        currentBloom = Mathf.Min(currentBloom + bloomIncreaseRate, maxBloomAngle);

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

        yield return new WaitForSeconds(reloadTime);

        // Return leftover bullets to reserve
        reserveAmmo += ammoLoaded;
        ammoLoaded = 0;

        // Fill magazine
        int ammoToLoad = Mathf.Min(magSize, reserveAmmo);
        ammoLoaded = ammoToLoad;
        reserveAmmo -= ammoToLoad;

        reloading = false;
        canShoot = true;
        needsReload = false;
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
        {
            controller.ApplyRecoilInstant(targetRecoilX, targetRecoilY);
        }

        // Always decay recoil gradually so consecutive shots stack naturally
        targetRecoilX = Mathf.MoveTowards(targetRecoilX, 0f, 10f * Time.deltaTime);
        targetRecoilY = Mathf.MoveTowards(targetRecoilY, 0f, 10f * Time.deltaTime);
    }
}
