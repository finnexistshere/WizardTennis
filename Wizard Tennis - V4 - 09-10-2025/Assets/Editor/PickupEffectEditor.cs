// Save this as: Assets/Editor/PickupEffectEditor.cs

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(PickupEffect))]
public class PickupEffectEditor : Editor
{
    private SerializedProperty spellAddressProp;
    private SerializedProperty defaultSpellNameProp;
    private SerializedProperty localizedSpellNamesProp;

    private bool showLocalizationFoldout = true;

    private void OnEnable()
    {
        spellAddressProp = serializedObject.FindProperty("spellAddress");
        defaultSpellNameProp = serializedObject.FindProperty("defaultSpellName");
        localizedSpellNamesProp = serializedObject.FindProperty("localizedSpellNames");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw default inspector for all other properties
        DrawPropertiesExcluding(serializedObject, "localizedSpellNames", "defaultSpellName");

        EditorGUILayout.Space(10);

        // Custom localization section
        EditorGUILayout.LabelField("Spell Localization", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(defaultSpellNameProp, new GUIContent("Default Spell Name", 
            "Fallback name if no translation exists"));

        EditorGUILayout.Space(5);
        
        showLocalizationFoldout = EditorGUILayout.Foldout(showLocalizationFoldout, 
            "Localized Spell Names", true, EditorStyles.foldoutHeader);

        if (showLocalizationFoldout)
        {
            EditorGUI.indentLevel++;

            // Quick setup buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add All Languages", GUILayout.Width(150)))
            {
                AddAllLanguages();
            }
            if (GUILayout.Button("Clear All", GUILayout.Width(150)))
            {
                if (EditorUtility.DisplayDialog("Clear All Localizations", 
                    "Are you sure you want to remove all localized names?", "Yes", "Cancel"))
                {
                    localizedSpellNamesProp.ClearArray();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Display existing localizations
            for (int i = 0; i < localizedSpellNamesProp.arraySize; i++)
            {
                SerializedProperty element = localizedSpellNamesProp.GetArrayElementAtIndex(i);
                SerializedProperty languageProp = element.FindPropertyRelative("language");
                SerializedProperty nameProp = element.FindPropertyRelative("localizedName");

                EditorGUILayout.BeginHorizontal();

                // Language dropdown
                Language currentLang = (Language)languageProp.enumValueIndex;
                EditorGUILayout.LabelField(LanguageHelper.GetLanguageName(currentLang), 
                    GUILayout.Width(100));

                // Name field
                nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);

                // Remove button
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    localizedSpellNamesProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            // Add new language dropdown
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Add Language:", GUILayout.Width(100));
            
            if (GUILayout.Button("Add...", GUILayout.Width(150)))
            {
                ShowAddLanguageMenu();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);

        // Info box
        EditorGUILayout.HelpBox(
            "Spell Address: " + spellAddressProp.stringValue + "\n" +
            "Default Name: " + defaultSpellNameProp.stringValue + "\n" +
            "Localizations: " + localizedSpellNamesProp.arraySize + " languages",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    private void AddAllLanguages()
    {
        // Get all available languages
        var allLanguages = System.Enum.GetValues(typeof(Language));
        var existingLanguages = new HashSet<Language>();

        // Track existing languages
        for (int i = 0; i < localizedSpellNamesProp.arraySize; i++)
        {
            SerializedProperty element = localizedSpellNamesProp.GetArrayElementAtIndex(i);
            SerializedProperty languageProp = element.FindPropertyRelative("language");
            existingLanguages.Add((Language)languageProp.enumValueIndex);
        }

        // Add missing languages
        string defaultName = defaultSpellNameProp.stringValue;
        foreach (Language lang in allLanguages)
        {
            if (!existingLanguages.Contains(lang))
            {
                int newIndex = localizedSpellNamesProp.arraySize;
                localizedSpellNamesProp.InsertArrayElementAtIndex(newIndex);
                
                SerializedProperty newElement = localizedSpellNamesProp.GetArrayElementAtIndex(newIndex);
                SerializedProperty languageProp = newElement.FindPropertyRelative("language");
                SerializedProperty nameProp = newElement.FindPropertyRelative("localizedName");

                languageProp.enumValueIndex = (int)lang;
                nameProp.stringValue = defaultName; // Start with default name
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void ShowAddLanguageMenu()
    {
        GenericMenu menu = new GenericMenu();
        var allLanguages = System.Enum.GetValues(typeof(Language));
        var existingLanguages = new HashSet<Language>();

        // Track existing languages
        for (int i = 0; i < localizedSpellNamesProp.arraySize; i++)
        {
            SerializedProperty element = localizedSpellNamesProp.GetArrayElementAtIndex(i);
            SerializedProperty languageProp = element.FindPropertyRelative("language");
            existingLanguages.Add((Language)languageProp.enumValueIndex);
        }

        // Add menu items for missing languages
        foreach (Language lang in allLanguages)
        {
            if (!existingLanguages.Contains(lang))
            {
                string langName = LanguageHelper.GetLanguageName(lang);
                menu.AddItem(new GUIContent(langName), false, () => AddLanguage(lang));
            }
            else
            {
                string langName = LanguageHelper.GetLanguageName(lang);
                menu.AddDisabledItem(new GUIContent(langName + " (already added)"));
            }
        }

        menu.ShowAsContext();
    }

    private void AddLanguage(Language language)
    {
        int newIndex = localizedSpellNamesProp.arraySize;
        localizedSpellNamesProp.InsertArrayElementAtIndex(newIndex);
        
        SerializedProperty newElement = localizedSpellNamesProp.GetArrayElementAtIndex(newIndex);
        SerializedProperty languageProp = newElement.FindPropertyRelative("language");
        SerializedProperty nameProp = newElement.FindPropertyRelative("localizedName");

        languageProp.enumValueIndex = (int)language;
        nameProp.stringValue = defaultSpellNameProp.stringValue;

        serializedObject.ApplyModifiedProperties();
    }
}
#endif