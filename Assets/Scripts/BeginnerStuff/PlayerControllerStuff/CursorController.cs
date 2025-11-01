using UnityEngine;

public class CursorController : MonoBehaviour
{
    private bool cursorLocked = true;

    private void Start()
    {
        UpdateCursorState();
    }

    private void Update()
    {
        // Unlock cursor on Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            cursorLocked = false;
            UpdateCursorState();
        }

        // Lock cursor when clicking back into the game
        if (Input.GetMouseButtonDown(2))
        {
            cursorLocked = true;
            UpdateCursorState();
        }
    }

    private void UpdateCursorState()
    {
        Cursor.visible = !cursorLocked;
        Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
    }
}
