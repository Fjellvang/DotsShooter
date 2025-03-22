using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class PowerUpButtonWithText : VisualElement
{
    private PowerupButton _powerupButton;
    private Label _label;
    private LabelContainer _costLabelContainer;
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
        _costLabelContainer = this.Q<LabelContainer>("CostLabel");
    }
    
    [UxmlAttribute]
    public string Label
    {
        get => _powerupButton.Label;
        set => _powerupButton.Label = value;
    }
    
    public Button Button => _powerupButton.Button;

    [UxmlAttribute]
    public float Value
    {
        get => _value;
        set
        {
            _value = value;
            _label.text = _value.ToString(CultureInfo.InvariantCulture);
        }
    }
    [UxmlAttribute]
    public float CostValue
    {
        get => _costLabelContainer.Value;
        set => _costLabelContainer.Value = value;
    }
}