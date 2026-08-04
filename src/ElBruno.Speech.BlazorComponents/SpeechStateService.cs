namespace ElBruno.Speech.BlazorComponents;

/// <summary>
/// Per-circuit state shared by the speech UI components.
/// </summary>
public sealed class SpeechStateService
{
    private readonly List<TranscriptSegment> _transcript = [];

    /// <summary>Raised when state consumed by a component changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Current state of the speech pipeline.</summary>
    public PipelineState CurrentState { get; private set; } = PipelineState.Idle;

    /// <summary>Current VAD probability in the range 0 to 1.</summary>
    public float VadProbability { get; private set; }

    /// <summary>Currently selected browser input device.</summary>
    public string? SelectedDeviceId { get; private set; }

    /// <summary>Transcript segments collected for this circuit.</summary>
    public IReadOnlyList<TranscriptSegment> Transcript => _transcript;

    /// <summary>Updates the pipeline state.</summary>
    public void SetState(PipelineState state)
    {
        if (CurrentState == state)
            return;

        CurrentState = state;
        NotifyChanged();
    }

    /// <summary>Updates the VAD probability, clamped to 0 through 1.</summary>
    public void SetVadProbability(float probability)
    {
        probability = Math.Clamp(probability, 0f, 1f);
        if (VadProbability.Equals(probability))
            return;

        VadProbability = probability;
        NotifyChanged();
    }

    /// <summary>Sets the selected browser input device.</summary>
    public void SetSelectedDevice(string? deviceId)
    {
        if (SelectedDeviceId == deviceId)
            return;

        SelectedDeviceId = deviceId;
        NotifyChanged();
    }

    /// <summary>Adds a transcript segment to the circuit state.</summary>
    public void AddTranscript(string text, bool isFinal = true, DateTime? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        _transcript.Add(new TranscriptSegment(text, timestamp ?? DateTime.Now, isFinal));
        NotifyChanged();
    }

    /// <summary>Clears the circuit transcript.</summary>
    public void ClearTranscript()
    {
        if (_transcript.Count == 0)
            return;

        _transcript.Clear();
        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
