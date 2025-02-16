using System;
using System.Collections;
using DotsShooter;
using DotsShooter.Gold;
using DotsShooter.Health;
using DotsShooter.Metaplay;
using DotsShooter.Player;
using DotsShooter.UI;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class GoldPresenter : MonoBehaviour
{
    [Tooltip("Reference to the View (UI)")] [SerializeField]
    private UIDocument m_Document;
    
    // Root element of the UI
    private VisualElement _root;

    private LabelContainer _goldLabel;

    private void Start()
    {
        _root = m_Document.rootVisualElement;
        _goldLabel = _root.Q<LabelContainer>("gold-label");
        UpdateGold();
    }

    public void UpdateGold()
    {
        _goldLabel.Value = MetaplayClient.PlayerModel.Gold;
    }
}