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

    // NEW: do not select anything until first keyboard input
    private bool selectionActivated = false;

    private void OnEnable()
    {
        selectionActivated = false;
        UpdateActivePanel();
    }

    private void Update()
    {
        if (currentPanel == null || !currentPanel.activeInHierarchy)
        {
            UpdateActivePanel();
            return;
        }

        if (currentButtons.Count == 0) return;

        bool up = Input.GetKeyDown(upKey);
        bool down = Input.GetKeyDown(downKey);
        bool confirm = Input.GetKeyDown(confirmKey);

        // FIRST INPUT ACTIVATES SELECTION
        if (!selectionActivated)
        {
            if (up || down || confirm)
            {
                selectionActivated = true;
                SelectButton(0);  // now we can highlight the first button
            }
            return; // ignore until first input happens
        }

        // Normal navigation after first activation
        if (up) MoveSelection(-1);
        else if (down) MoveSelection(1);
        else if (confirm) ConfirmSelection();
    }

    void UpdateActivePanel()
    {
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

                    // DO NOT select a button yet — wait until user presses a key
                    currentIndex = -1;
                    EventSystem.current.SetSelectedGameObject(null);
                    return;
                }
            }
        }

        currentPanel = null;
        currentButtons.Clear();
        currentIndex = -1;
    }

    void MoveSelection(int direction)
    {
        if (currentButtons.Count == 0) return;

        if (currentIndex >= 0)
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
