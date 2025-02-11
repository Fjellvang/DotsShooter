// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using UnityEngine;
using UnityEngine.UIElements;

namespace Metaplay.Unity
{
    static class TextFieldExtensions
    {
        public static void SetPlaceholderText(this TextField textField, string placeholder)
        {
            // Unity doesn't support placeholders pre-2023.1...
            string placeholderClass = TextField.ussClassName + "__placeholder";

            TextElement textElement   = textField.Q<TextElement>(className:"unity-text-element--inner-input-field-component");
            // Use the default editor color as original color
            Color       originalColor = new Color(196, 196, 196);

            OnFocusOut();
            textField.RegisterCallback<FocusInEvent>(evt => OnFocusIn());
            textField.RegisterCallback<FocusOutEvent>(evt => OnFocusOut());

            void OnFocusIn()
            {
                if (textField.ClassListContains(placeholderClass))
                {
                    textField.value       = string.Empty;
                    textField.RemoveFromClassList(placeholderClass);

                    if (textElement != null)
                        textElement.style.color = originalColor;
                }
            }

            void OnFocusOut()
            {
                if (string.IsNullOrEmpty(textField.text))
                {
                    textField.SetValueWithoutNotify(placeholder);
                    textField.AddToClassList(placeholderClass);

                    if (textElement != null)
                        textElement.style.color = Color.gray;
                }
            }
        }
    }
}
