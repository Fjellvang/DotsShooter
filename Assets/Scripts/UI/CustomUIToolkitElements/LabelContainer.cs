using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class LabelContainer : VisualElement
{
    private Label _valueLabel;
    private Label _textLabel;
    private float _value = 0;

    public LabelContainer()
    {
        // Load the UXML
        var visualTree = Resources.Load<VisualTreeAsset>("UI/LabelContainer");
        if (visualTree != null)
        {
            visualTree.CloneTree(this);
        }
        else
        {
            Debug.LogError("Failed to load Button.uxml");
        }
        
        _valueLabel = this.Q<Label>("ValueLabel");
        _textLabel = this.Q<Label>("TextLabel");
    }
    
    [UxmlAttribute]
    public string Text
    {
        get => _textLabel.text;
        set => _textLabel.text = value;
    }
    
    [UxmlAttribute]
    public float Value
    {
        get => _value;
        set
        {
            _value = value;
            _valueLabel.text = _value.ToString(CultureInfo.InvariantCulture);
        }
    }
}