using UnityEditor;
using UnityEngine;

namespace HHH.Common
{
    public class NamedIdAttribute : PropertyAttribute
    {
        public NamedIdAttribute()
        {
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(NamedIdAttribute))]
    public class NamedIdAttributeDrawer : PropertyDrawer
    {
        NamedIdAttribute TargetAttribute => attribute as NamedIdAttribute;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Context(position, property);

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            var id = property.stringValue;
            if (string.IsNullOrEmpty(id))
            {
                id = StringUtils.ToSnakeCase(property.serializedObject.targetObject.name);
                property.stringValue = id;
                property.serializedObject.ApplyModifiedProperties();
            }

            using (new EditorGUIUtils.DisabledGUI(true))
            {
                EditorGUI.TextField(position, id);
            }

            EditorGUI.EndProperty();
        }

        void Context(Rect rect, SerializedProperty property)
        {
            var current = Event.current;

            if (rect.Contains(current.mousePosition) && current.type == EventType.ContextClick)
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Reset"), false,
                    () =>
                    {
                        property.stringValue = StringUtils.ToSnakeCase(property.serializedObject.targetObject.name);
                        property.serializedObject.ApplyModifiedProperties();
                    });
                menu.ShowAsContext();

                current.Use();
            }
        }


    }
#endif

}