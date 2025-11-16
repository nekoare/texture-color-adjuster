using UnityEngine;
using UnityEditor;

namespace TexColAdjuster.Editor
{
    public class EditorInputDialog : EditorWindow
    {
        private string inputText = "";
        private string dialogTitle = "Input";
        private string dialogMessage = "Enter text:";
        private string defaultValue = "";
        private System.Action<string> onConfirm;
        private System.Action onCancel;
        
        public static string Show(string title, string message, string defaultValue = "")
        {
            var window = CreateInstance<EditorInputDialog>();
            window.dialogTitle = title;
            window.dialogMessage = message;
            window.defaultValue = defaultValue;
            window.inputText = defaultValue;
            window.titleContent = new GUIContent(title);
            window.minSize = new Vector2(300, 120);
            window.maxSize = new Vector2(400, 120);
            window.ShowModal();
            
            return window.inputText;
        }
        
        private void OnGUI()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField(dialogMessage, EditorStyles.wordWrappedLabel);
            GUILayout.Space(5);
            
            GUI.SetNextControlName("InputField");
            inputText = EditorGUILayout.TextField(inputText);
            
            if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "InputField")
            {
                GUI.FocusControl("InputField");
            }
            
            GUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button(LocalizationManager.Get("ok"), GUILayout.Width(60)))
            {
                onConfirm?.Invoke(inputText);
                Close();
            }
            
            if (GUILayout.Button(LocalizationManager.Get("cancel"), GUILayout.Width(60)))
            {
                inputText = "";
                onCancel?.Invoke();
                Close();
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                onConfirm?.Invoke(inputText);
                Close();
            }
            
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                inputText = "";
                onCancel?.Invoke();
                Close();
            }
        }
    }
}