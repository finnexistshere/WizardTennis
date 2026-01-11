#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class LocalizationToolsWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private bool autoSelectChildren = true;
    private bool includeInactive = true;
    private Language defaultLanguage = Language.English;

    private List<GameObject> selectedObjects = new List<GameObject>();
    private List<ConversionItem> conversionItems = new List<ConversionItem>();

    private class ConversionItem
    {
        public GameObject gameObject;
        public string currentText;
        public string componentType;
        public bool selected = true;
    }

    [MenuItem("Tools/Localization Tools")]
    public static void ShowWindow()
    {
        GetWindow<LocalizationToolsWindow>("Localization Tools");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Localization Batch Tools", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Convert existing UI elements to localized components in bulk.", MessageType.Info);

        EditorGUILayout.Space(10);

        // Settings
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        autoSelectChildren = EditorGUILayout.Toggle("Include Children", autoSelectChildren);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
        defaultLanguage = (Language)EditorGUILayout.EnumPopup("Default Language", defaultLanguage);

        EditorGUILayout.Space(10);

        // Scan Section
        EditorGUILayout.LabelField("Step 1: Scan Scene/Selection", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Scan Selected Objects", GUILayout.Height(30)))
        {
            ScanSelection();
        }

        if (GUILayout.Button("Scan Entire Scene", GUILayout.Height(30)))
        {
            ScanScene();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Results Section
        if (conversionItems.Count > 0)
        {
            EditorGUILayout.LabelField($"Step 2: Review ({conversionItems.Count} items found)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All"))
            {
                conversionItems.ForEach(item => item.selected = true);
            }
            if (GUILayout.Button("Deselect All"))
            {
                conversionItems.ForEach(item => item.selected = false);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

            foreach (var item in conversionItems)
            {
                EditorGUILayout.BeginHorizontal();
                item.selected = EditorGUILayout.Toggle(item.selected, GUILayout.Width(20));

                EditorGUILayout.LabelField(item.gameObject.name, GUILayout.Width(150));
                EditorGUILayout.LabelField($"[{item.componentType}]", GUILayout.Width(100));
                EditorGUILayout.LabelField(TruncateText(item.currentText, 50), GUILayout.Width(250));

                if (GUILayout.Button("Ping", GUILayout.Width(50)))
                {
                    EditorGUIUtility.PingObject(item.gameObject);
                    Selection.activeGameObject = item.gameObject;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Convert Section
            EditorGUILayout.LabelField("Step 3: Convert", EditorStyles.boldLabel);

            int selectedCount = conversionItems.Count(item => item.selected);
            EditorGUILayout.HelpBox($"{selectedCount} items selected for conversion", MessageType.None);

            if (GUILayout.Button($"Convert Selected Items ({selectedCount})", GUILayout.Height(40)))
            {
                ConvertSelectedItems();
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Clear Results", GUILayout.Height(25)))
            {
                conversionItems.Clear();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No items found. Use the scan buttons above to find UI elements.", MessageType.Warning);
        }

        EditorGUILayout.Space(10);

        // Quick Actions
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Add LocalizedText to Selected", GUILayout.Height(30)))
        {
            AddLocalizedTextToSelection();
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Remove All Localization Components from Selected", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Remove Localization", 
                "Remove all localization components from selected objects?", "Yes", "Cancel"))
            {
                RemoveLocalizationFromSelection();
            }
        }
    }

    private void ScanSelection()
    {
        conversionItems.Clear();

        foreach (var obj in Selection.gameObjects)
        {
            if (autoSelectChildren)
            {
                ScanGameObject(obj, true);
            }
            else
            {
                ScanGameObject(obj, false);
            }
        }

        Debug.Log($"[LocalizationTools] Found {conversionItems.Count} convertible UI elements");
    }

    private void ScanScene()
    {
        conversionItems.Clear();

        // Find all root objects
        var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (var obj in rootObjects)
        {
            ScanGameObject(obj, true);
        }

        Debug.Log($"[LocalizationTools] Found {conversionItems.Count} convertible UI elements in scene");
    }

    private void ScanGameObject(GameObject obj, bool recursive)
    {
        if (!includeInactive && !obj.activeInHierarchy)
            return;

        // Check if already has localization component
        if (obj.GetComponent<LocalizedText>() != null ||
            obj.GetComponent<LocalizedButton>() != null ||
            obj.GetComponent<LocalizedDropdown>() != null ||
            obj.GetComponent<LocalizedInputField>() != null)
        {
            // Skip objects that already have localization
            if (recursive)
            {
                foreach (Transform child in obj.transform)
                {
                    ScanGameObject(child.gameObject, true);
                }
            }
            return;
        }

        // Check for Text components
        var text = obj.GetComponent<Text>();
        if (text != null)
        {
            conversionItems.Add(new ConversionItem
            {
                gameObject = obj,
                currentText = text.text,
                componentType = "Text"
            });
        }

        // Check for TextMeshPro components
        var tmpText = obj.GetComponent<TextMeshProUGUI>();
        if (tmpText != null)
        {
            conversionItems.Add(new ConversionItem
            {
                gameObject = obj,
                currentText = tmpText.text,
                componentType = "TextMeshPro"
            });
        }

        // Check for Buttons with text children
        var button = obj.GetComponent<Button>();
        if (button != null)
        {
            var buttonText = obj.GetComponentInChildren<Text>();
            var buttonTmpText = obj.GetComponentInChildren<TextMeshProUGUI>();

            if (buttonText != null || buttonTmpText != null)
            {
                string currentText = buttonText != null ? buttonText.text : buttonTmpText.text;
                conversionItems.Add(new ConversionItem
                {
                    gameObject = obj,
                    currentText = currentText,
                    componentType = "Button"
                });
            }
        }

        // Check for Dropdowns
        var dropdown = obj.GetComponent<Dropdown>();
        var tmpDropdown = obj.GetComponent<TMP_Dropdown>();
        if (dropdown != null || tmpDropdown != null)
        {
            conversionItems.Add(new ConversionItem
            {
                gameObject = obj,
                currentText = dropdown != null ? $"{dropdown.options.Count} options" : $"{tmpDropdown.options.Count} options",
                componentType = "Dropdown"
            });
        }

        // Check for InputFields
        var inputField = obj.GetComponent<InputField>();
        var tmpInputField = obj.GetComponent<TMP_InputField>();
        if (inputField != null || tmpInputField != null)
        {
            string placeholder = "";
            if (inputField != null && inputField.placeholder != null)
            {
                var placeholderText = inputField.placeholder.GetComponent<Text>();
                if (placeholderText != null)
                    placeholder = placeholderText.text;
            }
            else if (tmpInputField != null && tmpInputField.placeholder != null)
            {
                var placeholderText = tmpInputField.placeholder.GetComponent<TextMeshProUGUI>();
                if (placeholderText != null)
                    placeholder = placeholderText.text;
            }

            conversionItems.Add(new ConversionItem
            {
                gameObject = obj,
                currentText = placeholder,
                componentType = "InputField"
            });
        }

        // Recursively scan children
        if (recursive)
        {
            foreach (Transform child in obj.transform)
            {
                ScanGameObject(child.gameObject, true);
            }
        }
    }

    private void ConvertSelectedItems()
    {
        int convertedCount = 0;

        foreach (var item in conversionItems.Where(i => i.selected))
        {
            switch (item.componentType)
            {
                case "Text":
                case "TextMeshPro":
                    AddLocalizedTextComponent(item.gameObject, item.currentText);
                    convertedCount++;
                    break;

                case "Button":
                    AddLocalizedButtonComponent(item.gameObject, item.currentText);
                    convertedCount++;
                    break;

                case "Dropdown":
                    AddLocalizedDropdownComponent(item.gameObject);
                    convertedCount++;
                    break;

                case "InputField":
                    AddLocalizedInputFieldComponent(item.gameObject, item.currentText);
                    convertedCount++;
                    break;
            }
        }

        Debug.Log($"[LocalizationTools] Converted {convertedCount} UI elements");
        EditorUtility.DisplayDialog("Conversion Complete", 
            $"Successfully converted {convertedCount} UI elements to localized components.", "OK");

        // Rescan to update the list
        if (Selection.gameObjects.Length > 0)
            ScanSelection();
        else
            conversionItems.Clear();
    }

    private void AddLocalizedTextComponent(GameObject obj, string currentText)
    {
        var localizedText = obj.GetComponent<LocalizedText>();
        if (localizedText == null)
        {
            localizedText = obj.AddComponent<LocalizedText>();
        }

        localizedText.SetDefaultText(currentText);
        localizedText.SetLocalizedText(defaultLanguage, currentText);

        EditorUtility.SetDirty(obj);
        Debug.Log($"[LocalizationTools] Added LocalizedText to {obj.name}");
    }

    private void AddLocalizedButtonComponent(GameObject obj, string currentText)
    {
        var localizedButton = obj.GetComponent<LocalizedButton>();
        if (localizedButton == null)
        {
            localizedButton = obj.AddComponent<LocalizedButton>();
        }

        localizedButton.SetLocalizedText(defaultLanguage, currentText);

        EditorUtility.SetDirty(obj);
        Debug.Log($"[LocalizationTools] Added LocalizedButton to {obj.name}");
    }

    private void AddLocalizedDropdownComponent(GameObject obj)
    {
        var localizedDropdown = obj.GetComponent<LocalizedDropdown>();
        if (localizedDropdown == null)
        {
            localizedDropdown = obj.AddComponent<LocalizedDropdown>();
        }

        EditorUtility.SetDirty(obj);
        Debug.Log($"[LocalizationTools] Added LocalizedDropdown to {obj.name}");
    }

    private void AddLocalizedInputFieldComponent(GameObject obj, string placeholder)
    {
        var localizedInputField = obj.GetComponent<LocalizedInputField>();
        if (localizedInputField == null)
        {
            localizedInputField = obj.AddComponent<LocalizedInputField>();
        }

        localizedInputField.SetLocalizedPlaceholder(defaultLanguage, placeholder);

        EditorUtility.SetDirty(obj);
        Debug.Log($"[LocalizationTools] Added LocalizedInputField to {obj.name}");
    }

    private void AddLocalizedTextToSelection()
    {
        int addedCount = 0;

        foreach (var obj in Selection.gameObjects)
        {
            var text = obj.GetComponent<Text>();
            var tmpText = obj.GetComponent<TextMeshProUGUI>();

            if (text != null || tmpText != null)
            {
                string currentText = text != null ? text.text : tmpText.text;
                AddLocalizedTextComponent(obj, currentText);
                addedCount++;
            }
        }

        Debug.Log($"[LocalizationTools] Added LocalizedText to {addedCount} objects");
    }

    private void RemoveLocalizationFromSelection()
    {
        int removedCount = 0;

        foreach (var obj in Selection.gameObjects)
        {
            var components = obj.GetComponents<Component>();

            foreach (var component in components)
            {
                if (component is LocalizedText || 
                    component is LocalizedButton || 
                    component is LocalizedDropdown || 
                    component is LocalizedInputField)
                {
                    DestroyImmediate(component);
                    removedCount++;
                }
            }

            EditorUtility.SetDirty(obj);
        }

        Debug.Log($"[LocalizationTools] Removed {removedCount} localization components");
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return "[Empty]";

        text = text.Replace("\n", " ").Replace("\r", "");

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }
}
#endif