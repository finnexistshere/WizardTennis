#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

[CustomEditor(typeof(LocalizedText))]
public class LocalizedTextEditor : Editor
{
    private SerializedProperty defaultTextProp;
    private SerializedProperty localizedTextsProp;
    private SerializedProperty legacyTextProp;
    private SerializedProperty tmpTextProp;
    private SerializedProperty tmp3DTextProp;
    private SerializedProperty updateOnStartProp;
    private SerializedProperty useFormattingProp;

    private bool showLocalizationFoldout = true;
    private bool showComponentsFoldout = false;
    private Language previewLanguage = Language.English;

    private void OnEnable()
    {
        defaultTextProp = serializedObject.FindProperty("defaultText");
        localizedTextsProp = serializedObject.FindProperty("localizedTexts");
        legacyTextProp = serializedObject.FindProperty("legacyText");
        tmpTextProp = serializedObject.FindProperty("tmpText");
        tmp3DTextProp = serializedObject.FindProperty("tmp3DText");
        updateOnStartProp = serializedObject.FindProperty("updateOnStart");
        useFormattingProp = serializedObject.FindProperty("useFormatting");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedText localizedText = (LocalizedText)target;

        // Header
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Localized Text Component", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This component automatically updates text when the language changes in Options.", MessageType.Info);

        EditorGUILayout.Space(5);

        // Default Text
        EditorGUILayout.LabelField("Default Text", EditorStyles.boldLabel);
        defaultTextProp.stringValue = EditorGUILayout.TextArea(defaultTextProp.stringValue, GUILayout.Height(40));
        
        EditorGUILayout.Space(5);

        // Advanced Options
        EditorGUILayout.PropertyField(updateOnStartProp, new GUIContent("Update On Start"));
        EditorGUILayout.PropertyField(useFormattingProp, new GUIContent("Use String Formatting", 
            "Enable for format strings like 'Score: {0}'"));

        EditorGUILayout.Space(10);

        // Localizations Section
        showLocalizationFoldout = EditorGUILayout.Foldout(showLocalizationFoldout, 
            $"Localizations ({localizedTextsProp.arraySize})", true, EditorStyles.foldoutHeader);

        if (showLocalizationFoldout)
        {
            EditorGUI.indentLevel++;

            // Quick Setup Buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add All Languages", GUILayout.Width(130)))
            {
                AddAllLanguages();
            }
            if (GUILayout.Button("Copy from Default", GUILayout.Width(130)))
            {
                CopyFromDefault();
            }
            if (GUILayout.Button("Clear All", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("Clear All Localizations", 
                    "Are you sure you want to remove all localized texts?", "Yes", "Cancel"))
                {
                    localizedTextsProp.ClearArray();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Preview Language Selector
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Preview Language:", GUILayout.Width(110));
            previewLanguage = (Language)EditorGUILayout.EnumPopup(previewLanguage);
            if (GUILayout.Button("Preview", GUILayout.Width(80)))
            {
                PreviewLanguage(previewLanguage);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Display existing localizations with better formatting
            for (int i = 0; i < localizedTextsProp.arraySize; i++)
            {
                SerializedProperty element = localizedTextsProp.GetArrayElementAtIndex(i);
                SerializedProperty languageProp = element.FindPropertyRelative("language");
                SerializedProperty textProp = element.FindPropertyRelative("text");

                Language lang = (Language)languageProp.enumValueIndex;

                // Language header with remove button
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"?? {LanguageHelper.GetLanguageName(lang)}", 
                    EditorStyles.boldLabel, GUILayout.Width(120));
                
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("×", GUILayout.Width(25), GUILayout.Height(18)))
                {
                    localizedTextsProp.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();

                // Text area
                EditorGUI.indentLevel++;
                textProp.stringValue = EditorGUILayout.TextArea(textProp.stringValue, GUILayout.Height(60));
                EditorGUI.indentLevel--;

                // Character count
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Characters: {textProp.stringValue.Length}", 
                    EditorStyles.miniLabel, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
            }

            // Add new language button
            EditorGUILayout.Space(5);
            if (GUILayout.Button("+ Add Language Translation", GUILayout.Height(25)))
            {
                ShowAddLanguageMenu();
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);

        // Component References Section
        showComponentsFoldout = EditorGUILayout.Foldout(showComponentsFoldout, 
            "Component References (Auto-Detected)", true, EditorStyles.foldoutHeader);

        if (showComponentsFoldout)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(legacyTextProp, new GUIContent("Legacy Text"));
            EditorGUILayout.PropertyField(tmpTextProp, new GUIContent("TextMeshPro UGUI"));
            EditorGUILayout.PropertyField(tmp3DTextProp, new GUIContent("TextMeshPro 3D"));

            // Auto-detect button
            if (GUILayout.Button("Auto-Detect Components", GUILayout.Height(25)))
            {
                AutoDetectComponents();
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);

        // Info Box
        string currentText = localizedText.GetCurrentText();
        int translationCount = localizedTextsProp.arraySize;
        EditorGUILayout.HelpBox(
            $"Current Text Length: {currentText.Length} characters\n" +
            $"Translations: {translationCount}/{System.Enum.GetValues(typeof(Language)).Length} languages",
            MessageType.None);

        // Test buttons
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Update Text Now"))
        {
            localizedText.UpdateText();
            EditorUtility.SetDirty(target);
        }
        if (GUILayout.Button("Copy Default to Current"))
        {
            if (localizedText.GetComponent<TextMeshProUGUI>() != null)
                defaultTextProp.stringValue = localizedText.GetComponent<TextMeshProUGUI>().text;
            else if (localizedText.GetComponent<Text>() != null)
                defaultTextProp.stringValue = localizedText.GetComponent<Text>().text;
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    private void AddAllLanguages()
    {
        var allLanguages = System.Enum.GetValues(typeof(Language));
        var existingLanguages = new HashSet<Language>();

        // Track existing languages
        for (int i = 0; i < localizedTextsProp.arraySize; i++)
        {
            SerializedProperty element = localizedTextsProp.GetArrayElementAtIndex(i);
            SerializedProperty languageProp = element.FindPropertyRelative("language");
            existingLanguages.Add((Language)languageProp.enumValueIndex);
        }

        // Add missing languages
        string defaultText = defaultTextProp.stringValue;
        foreach (Language lang in allLanguages)
        {
            if (!existingLanguages.Contains(lang))
            {
                int newIndex = localizedTextsProp.arraySize;
                localizedTextsProp.InsertArrayElementAtIndex(newIndex);
                
                SerializedProperty newElement = localizedTextsProp.GetArrayElementAtIndex(newIndex);
                SerializedProperty languageProp = newElement.FindPropertyRelative("language");
                SerializedProperty textProp = newElement.FindPropertyRelative("text");

                languageProp.enumValueIndex = (int)lang;
                textProp.stringValue = defaultText;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void CopyFromDefault()
    {
        string defaultText = defaultTextProp.stringValue;

        for (int i = 0; i < localizedTextsProp.arraySize; i++)
        {
            SerializedProperty element = localizedTextsProp.GetArrayElementAtIndex(i);
            SerializedProperty textProp = element.FindPropertyRelative("text");
            textProp.stringValue = defaultText;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void ShowAddLanguageMenu()
    {
        GenericMenu menu = new GenericMenu();
        var allLanguages = System.Enum.GetValues(typeof(Language));
        var existingLanguages = new HashSet<Language>();

        // Track existing languages
        for (int i = 0; i < localizedTextsProp.arraySize; i++)
        {
            SerializedProperty element = localizedTextsProp.GetArrayElementAtIndex(i);
            SerializedProperty languageProp = element.FindPropertyRelative("language");
            existingLanguages.Add((Language)languageProp.enumValueIndex);
        }

        // Add menu items
        foreach (Language lang in allLanguages)
        {
            string langName = LanguageHelper.GetLanguageName(lang);
            if (!existingLanguages.Contains(lang))
            {
                menu.AddItem(new GUIContent(langName), false, () => AddLanguage(lang));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(langName + " ?"));
            }
        }

        menu.ShowAsContext();
    }

    private void AddLanguage(Language language)
    {
        int newIndex = localizedTextsProp.arraySize;
        localizedTextsProp.InsertArrayElementAtIndex(newIndex);
        
        SerializedProperty newElement = localizedTextsProp.GetArrayElementAtIndex(newIndex);
        SerializedProperty languageProp = newElement.FindPropertyRelative("language");
        SerializedProperty textProp = newElement.FindPropertyRelative("text");

        languageProp.enumValueIndex = (int)language;
        textProp.stringValue = defaultTextProp.stringValue;

        serializedObject.ApplyModifiedProperties();
    }

    private void AutoDetectComponents()
    {
        LocalizedText localizedText = (LocalizedText)target;

        legacyTextProp.objectReferenceValue = localizedText.GetComponent<Text>();
        tmpTextProp.objectReferenceValue = localizedText.GetComponent<TextMeshProUGUI>();
        tmp3DTextProp.objectReferenceValue = localizedText.GetComponent<TextMeshPro>();

        serializedObject.ApplyModifiedProperties();

        Debug.Log($"[LocalizedText] Auto-detected components on {localizedText.gameObject.name}");
    }

    private void PreviewLanguage(Language language)
    {
        LocalizedText localizedText = (LocalizedText)target;
        string text = localizedText.GetLocalizedText(language);

        // Temporarily set the text
        if (localizedText.GetComponent<TextMeshProUGUI>() != null)
            localizedText.GetComponent<TextMeshProUGUI>().text = text;
        else if (localizedText.GetComponent<Text>() != null)
            localizedText.GetComponent<Text>().text = text;
        else if (localizedText.GetComponent<TextMeshPro>() != null)
            localizedText.GetComponent<TextMeshPro>().text = text;

        EditorUtility.SetDirty(target);
        Debug.Log($"[LocalizedText] Previewing {LanguageHelper.GetLanguageName(language)}: {text}");
    }
}
#endif