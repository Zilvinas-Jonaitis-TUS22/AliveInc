using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class FlexiShootingScript : NetworkBehaviour
{
    [Header("Networking / Control")]
    public bool useNetworking = true;

    [Header("Gun State")]
    public bool isEquipped = false;
    public bool canShoot = true;
    public bool needsReload = false;
    public bool isReloading = false;

    [Header("Ammo")]
    public int ammoLoaded = 30;
    public int reserveAmmo = 90;
    public int magazineSize = 30;
    public float reloadTime = 2f;

    [Header("Gun Settings")]
    public float fireRate = 0.1f;
    public float damage = 10f;
    public Transform firePoint;
    public float range = 100f;
    public bool isAutomatic = true;

    private StarterAssetsInputs input;
    private float lastShotTime;
    private Coroutine reloadCoroutine;

    private void Awake()
    {
        input = GetComponent<StarterAssetsInputs>();
    }

    private void Update()
    {
        if (useNetworking && !IsOwner) return;
        if (!isEquipped) return;

        HandleShooting();
        HandleReloading();
    }

    private void HandleShooting()
    {
        if (!canShoot || isReloading) return;

        bool shouldShoot = isAutomatic ? input.shoot : input.shoot && Time.time - lastShotTime >= fireRate;

        if (shouldShoot && Time.time - lastShotTime >= fireRate)
        {
            if (ammoLoaded <= 0)
            {
                needsReload = true;
                return;
            }

            // Cancel reload if shooting mid-reload
            if (isReloading)
            {
                CancelReload();
            }

            FireRaycast();
            ammoLoaded--;
            lastShotTime = Time.time;

            // Only set needsReload when magazine actually empty
            if (ammoLoaded <= 0)
                needsReload = true;
        }
    }

    private void FireRaycast()
    {
        if (firePoint == null) return;

        Ray ray = new Ray(firePoint.position, firePoint.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            Debug.Log($"Hit {hit.collider.name} for {damage} damage!");

            var health = hit.collider.GetComponent<Health>();
            if (health != null)
                health.TakeDamage(damage);
        }

        Debug.DrawRay(firePoint.position, firePoint.forward * range, Color.red, 1f);
    }

    private void HandleReloading()
    {
        // Only start reload if not already reloading, reserve ammo exists, and reload requested
        if ((input.reload || needsReload) && !isReloading && reserveAmmo > 0)
        {
            needsReload = false; // Reset to prevent auto-restart
            reloadCoroutine = StartCoroutine(ReloadCoroutine());
        }
    }

    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        canShoot = false;
        Debug.Log("Reloading...");

        float timer = 0f;
        while (timer < reloadTime)
        {
            // Cancel reload if shoot input is pressed
            if (input.shoot)
            {
                Debug.Log("Reload canceled!");
                CancelReload();
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // Add leftover bullets to reserve only when reload finishes
        reserveAmmo += ammoLoaded;

        // Fill magazine from reserve
        int ammoToLoad = Mathf.Min(magazineSize, reserveAmmo);
        reserveAmmo -= ammoToLoad;
        ammoLoaded = ammoToLoad;

        isReloading = false;
        canShoot = true;

        // Only set needsReload if magazine still empty
        needsReload = ammoLoaded <= 0;

        Debug.Log($"Reload complete. Ammo loaded: {ammoLoaded}, Reserve: {reserveAmmo}");
    }

    private void CancelReload()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        isReloading = false;
        canShoot = true;

        // Ammo stays in magazine, reserve unchanged
    }

    public void SetEquipped(bool equipped)
    {
        isEquipped = equipped;
    }

    public void Fire()
    {
        input.shoot = true;
        HandleShooting();
        input.shoot = false;
    }
}
