using System.Collections;
using DotsShooter;
using DotsShooter.Gold;
using DotsShooter.Health;
using DotsShooter.Player;
using DotsShooter.UI;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class GoldPresenter : MonoBehaviour
{
    [Tooltip("Reference to the View (UI)")] [SerializeField]
    private UIDocument m_Document;
    [SerializeField]
    private PlayerGold _playerGold;
    
    // Root element of the UI
    private VisualElement _root;

    private LabelContainer _goldLabel;

    private void Start()
    {
        _root = m_Document.rootVisualElement;
        _goldLabel = _root.Q<LabelContainer>("gold-label");
        _goldLabel.Value = _playerGold.Gold;
    }


    private void OnEnable()
    {
        _playerGold.OnGoldChanged += OnGoldChanged;
    }

    private void OnDisable()
    {
        _playerGold.OnGoldChanged -= OnGoldChanged;
    }

    public void OnGoldChanged()
    {
        _goldLabel.Value = _playerGold.Gold;
    }
}