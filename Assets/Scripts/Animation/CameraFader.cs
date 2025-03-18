using System.Collections;
using DotsShooter;
using DotsShooter.Events;
using UnityEngine;

public class CameraFader : MonoBehaviour
{
    [SerializeField] private Material _fadeMaterial;
    [SerializeField] private float _fadeInDuration = 2f;
    [SerializeField] private float _fadeOutDuration = 2f;
    [SerializeField] private AnimationCurve _fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve _fadeOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private Color _originalColor;
    private Coroutine _activeFadeCoroutine;
    private EventSystem _eventSystem;

    private void Awake()
    {
        _originalColor = _fadeMaterial.color;
        // Start with fully opaque black
        SetAlpha(1f);
    }

    private IEnumerator Start()
    {
        // Wait for event system to be available
        while (!Helpers.TryGetEventSystem(out _eventSystem))
        {
            yield return null;
        }
        
        _eventSystem.OnPlayerWon += FadeOut;
        
        // Initial fade in when scene starts
        FadeIn();
    }

    private void OnDisable()
    {
        if (_eventSystem != null)
        {
            _eventSystem.OnPlayerWon -= FadeOut;
        }
    }
    
    public void FadeIn()
    {
        StopActiveFade();
        _activeFadeCoroutine = StartCoroutine(FadeCoroutine(1f, 0f, _fadeInDuration, _fadeInCurve));
    }

    public void FadeOut()
    {
        StopActiveFade();
        _activeFadeCoroutine = StartCoroutine(FadeCoroutine(0f, 1f, _fadeOutDuration, _fadeOutCurve));
    }
    
    /// <summary>
    /// Fades alpha from startAlpha to endAlpha over duration following the curve
    /// </summary>
    private IEnumerator FadeCoroutine(float startAlpha, float endAlpha, float duration, AnimationCurve curve)
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            float normalizedTime = elapsedTime / duration;
            float curveValue = curve.Evaluate(normalizedTime);
            float alpha = Mathf.Lerp(startAlpha, endAlpha, curveValue);
            
            SetAlpha(alpha);
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Ensure we end at exactly the target alpha
        SetAlpha(endAlpha);
        _activeFadeCoroutine = null;
    }
    
    private void SetAlpha(float alpha)
    {
        _fadeMaterial.color = new Color(_originalColor.r, _originalColor.g, _originalColor.b, alpha);
    }
    
    private void StopActiveFade()
    {
        if (_activeFadeCoroutine != null)
        {
            StopCoroutine(_activeFadeCoroutine);
            _activeFadeCoroutine = null;
        }
    }
    
    // Call this to fade with custom duration and curve
    public void FadeCustom(bool fadeIn, float duration, AnimationCurve curve = null)
    {
        if (fadeIn)
        {
            StopActiveFade();
            _activeFadeCoroutine = StartCoroutine(FadeCoroutine(1f, 0f, duration, curve ?? _fadeInCurve));
        }
        else
        {
            StopActiveFade();
            _activeFadeCoroutine = StartCoroutine(FadeCoroutine(0f, 1f, duration, curve ?? _fadeOutCurve));
        }
    }
}