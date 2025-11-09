using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UINavigationController : MonoBehaviour
{
    [Header("Input Settings")]
    public KeyCode upKey = KeyCode.UpArrow;
    public KeyCode downKey = KeyCode.DownArrow;
    public KeyCode confirmKey = KeyCode.E;

    private List<Button> currentButtons = new List<Button>();
    private int currentIndex = -1;
    private GameObject currentPanel;

    private void OnEnable()
    {
        UpdateActivePanel();
    }

    private void Update()
    {
        // If the active panel changes (eg. switching menus)
        if (currentPanel == null || !currentPanel.activeInHierarchy)
        {
            UpdateActivePanel();
            return;
        }

        if (currentButtons.Count == 0) return;

        if (Input.GetKeyDown(upKey))
            MoveSelection(-1);
        else if (Input.GetKeyDown(downKey))
            MoveSelection(1);
        else if (Input.GetKeyDown(confirmKey))
            ConfirmSelection();
    }

    void UpdateActivePanel()
    {
        // Find the first active canvas or panel containing buttons
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.gameObject.activeInHierarchy)
            {
                Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
                List<Button> activeButtons = new List<Button>();

                foreach (Button b in buttons)
                {
                    if (b.gameObject.activeInHierarchy && b.interactable)
                        activeButtons.Add(b);
                }

                if (activeButtons.Count > 0)
                {
                    currentPanel = canvas.gameObject;
                    currentButtons = activeButtons;
                    SelectButton(0);
                    return;
                }
            }
        }

        // No active buttons found
        currentPanel = null;
        currentButtons.Clear();
        currentIndex = -1;
    }

    void MoveSelection(int direction)
    {
        if (currentButtons.Count == 0) return;

        // Exit current hover
        TriggerPointerExit(currentButtons[currentIndex]);

        currentIndex += direction;

        if (currentIndex < 0) currentIndex = currentButtons.Count - 1;
        if (currentIndex >= currentButtons.Count) currentIndex = 0;

        SelectButton(currentIndex);
    }

    void SelectButton(int index)
    {
        currentIndex = index;
        Button button = currentButtons[currentIndex];

        EventSystem.current.SetSelectedGameObject(button.gameObject);
        TriggerPointerEnter(button);
    }

    void ConfirmSelection()
    {
        if (currentIndex < 0 || currentIndex >= currentButtons.Count) return;

        Button button = currentButtons[currentIndex];
        button.onClick.Invoke();
    }

    void TriggerPointerEnter(Button button)
    {
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
    }

    void TriggerPointerExit(Button button)
    {
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
    }
}
