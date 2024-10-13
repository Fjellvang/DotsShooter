using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class PowerupButton : VisualElement
{
    private Label _label;
    private Button _button;

    public PowerupButton(string label) : this()
    {
        Label = label;
    }

    public PowerupButton()
    {
        // Load the UXML
        var visualTree = Resources.Load<VisualTreeAsset>("UI/Powerups/PowerUpButton");
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
        get => Button.text;
        set => Button.text = value;
    }
    
    public Button Button => _button ??= this.Q<Button>("button");
}