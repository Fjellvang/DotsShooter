// namespace DotsShooter.UI.CustomUIToolkitElements
// { // TODO: Couldnt get namespaces workinig with UXML, so I commented it out

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


[UxmlElement("CustomButton")]
public partial class CustomButton : VisualElement
{

    private Label _label;
    private Button _button;

    public CustomButton()
    {
        // Load the UXML
        var visualTree = Resources.Load<VisualTreeAsset>("UI/Button/Button");
        if (visualTree != null)
        {
            visualTree.CloneTree(this);
            _label = this.Q<Label>("button-label");
        }
        else
        {
            Debug.LogError("Failed to load Button.uxml");
            // Fallback creation of elements
            _label = new Label("Button");
            Add(_label);
        }
    }
    
    [UxmlAttribute]
    public string Label
    {
        get => _label.text;
        set => _label.text = value;
    }
    
    public Button Button => _button ??= this.Q<Button>("button");
}

// }