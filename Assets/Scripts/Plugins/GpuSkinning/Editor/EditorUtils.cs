using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class EditorUtils
{
    static readonly GUIStyle k_BoldLabel = new GUIStyle("BoldLabel");
    static readonly GUIStyle k_HelpBox = new GUIStyle("HelpBox");
    // +
    static readonly GUIContent k_ToolbarPlus = EditorGUIUtility.TrIconContent("Toolbar Plus", "Add to the list");
    // -
    static readonly GUIContent k_ToolbarMinus = EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove selection from the list");

    
    public static void CreateListField<T>(List<T> list, Action<int,T> drawItemFunc) where T : class, new()
    {
        GUILayout.BeginVertical(k_HelpBox);
        {
            Type type = typeof(T);
            if(list.Count > 0)
            {
                for (int i = 0; i < list.Count; ++i)
                {
                    GUILayout.BeginVertical(k_HelpBox);
                    {
                        GUILayout.TextField($"元素[{i}]", k_BoldLabel);

                        // 绘制item
                        drawItemFunc?.Invoke(i, list[i]);
                    }
                    GUILayout.EndVertical();
                }
            }
            else
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("<空>", EditorStyles.label, GUILayout.Width(30));
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndVertical();

        GUILayout.BeginHorizontal();
        {
            GUILayout.FlexibleSpace();
            // +
            if (GUILayout.Button(k_ToolbarPlus, EditorStyles.miniButtonLeft, GUILayout.Width(30)))
            {
                list.Add(null);
            }
            
            // -
            if (GUILayout.Button(k_ToolbarMinus, EditorStyles.miniButtonRight, GUILayout.Width(30)))
            {
                if (list.Count > 0)
                {
                    list.RemoveAt(list.Count-1);
                }
            }
        }
        GUILayout.EndHorizontal();
    }
}
