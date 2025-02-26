using System;
using Sandbox;

public sealed class CameraShake : Component
{
    private Vector3 _originalPosition;
    private float _shakeTimeRemaining = 0f; // Time left for the shake
    private float _shakeMagnitude;
    private float _shakeDuration;
    private float _shakeDecay; // Rate at which the shake decays (lower value means slower decay)
    private bool _applyDecay; // Whether or not to apply damping

    protected override void OnStart()
    {
        _originalPosition = GameObject.WorldPosition; // Store the original position of the camera
    }

    // Camera shake logic with decay control
    public void Shake(float magnitude = 0.7f, float duration = 0.5f, bool decay = false)
    {
        _shakeMagnitude = magnitude;
        _shakeDuration = duration;
        _shakeTimeRemaining = duration; // Set the shake duration
        _applyDecay = decay; // Store the decay flag
    }

    Random Rand = new Random();
    protected override void OnUpdate()
    {
        // Apply the shake effect if there is remaining shake time
        if (_shakeTimeRemaining > 0)
        {
            // Apply random offsets in the X and Y axis, with damping applied to the magnitude if decay is enabled
            float shakeX = Rand.Float(-_shakeMagnitude, _shakeMagnitude);
            float shakeY = Rand.Float(-_shakeMagnitude, _shakeMagnitude);

            // Update the camera's position to create the shake
            GameObject.WorldPosition = _originalPosition + new Vector3(shakeX, shakeY, 0);

            // Decrease the remaining shake time
            _shakeTimeRemaining -= Time.Delta;

            if (_applyDecay)
            {
                // Apply damping: Reduce the magnitude over time
                _shakeMagnitude = Math.Max(_shakeMagnitude - _shakeDecay * Time.Delta, 0); // Ensure the magnitude doesn't go negative
                _shakeDecay = _shakeMagnitude / _shakeTimeRemaining * 5; // Update decay rate
            }

            // If the shake duration is over, reset the camera position
            if (_shakeTimeRemaining <= 0)
            {
                GameObject.WorldPosition = _originalPosition; // Reset to the original position
            }
        }
    }

    [Button("Shake Camera")]
    private void ShakeCamera()
    {
        Shake(magnitude: 2, decay: true); // Trigger a shake with decay enabled
    }
}
