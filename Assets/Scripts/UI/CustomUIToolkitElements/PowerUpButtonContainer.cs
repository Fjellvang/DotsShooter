using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class PowerUpButtonContainer : VisualElement
{
    private PowerupButton[] _powerupButtons;
    private string _label = "test";
    public PowerUpButtonContainer()
    {
        // Load the UXML
        var visualTree = Resources.Load<VisualTreeAsset>("UI/Powerups/PowerUpWindow");
        if (visualTree != null)
        {
            visualTree.CloneTree(this);
        }
        else
        {
            Debug.LogError("Failed to load Button.uxml");
        }
    }
    
    [UxmlAttribute]
    public string Label
    {
        get => _label;
        set
        {
            _label = value;
            var buttons = this.Query<PowerupButton>(className: "power-up-button");
            foreach (var button in buttons.ToList())
            {
                button.visible = true;
                button.style.display = DisplayStyle.Flex;
                button.Label = value;
            }
        }
    }
}