using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class PowerUpButtonWithText : VisualElement
{
    private PowerupButton _powerupButton;
    private Label _label;
    private float _value = 0;
    
    public PowerUpButtonWithText()
    {
        // Load the UXML
        var visualTree = Resources.Load<VisualTreeAsset>("UI/Powerups/PowerUpButtonWithText");
        if (visualTree != null)
        {
            visualTree.CloneTree(this);
        }
        else
        {
            Debug.LogError("Failed to load Button.uxml");
        }
        _powerupButton = this.Q<PowerupButton>("PowerupButton");
        _label = this.Q<Label>("ValueLabel");
    }
    
    [UxmlAttribute]
    public string Label
    {
        get => _powerupButton.Label;
        set => _powerupButton.Label = value;
    }

    [UxmlAttribute]
    public float Value
    {
        get => _value;
        set
        {
            _value = value;
            _label.text = $"Current: {value}"; //TODO: not best to hardcode. but for now it will make do.
        }
    }
}