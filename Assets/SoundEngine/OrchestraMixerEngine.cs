using UnityEngine;

public class OrchestraMixerEngine : MonoBehaviour
{
    [Header("Audio Sources (assign in inspector)")]
    public AudioSource strings;
    public AudioSource brass;
    public AudioSource woods;
    public AudioSource perc;

    [Header("Dynamics")]
    public Transform wandTip;
    public float dynamicsSensitivity = 0.4f;
    public float smoothing = 6f;

    [Header("Beat Accent")]
    public float beatThreshold = 1.3f;
    public float accentStrength = 0.25f;

    Vector3 lastPos;
    float dynamics;
    float accent;

    void Start()
    {
        lastPos = wandTip.position;

        StartAligned();
    }

    void StartAligned()
    {
        double startTime = AudioSettings.dspTime + 0.2;

        Prepare(strings);
        Prepare(brass);
        Prepare(woods);
        Prepare(perc);

        strings.PlayScheduled(startTime);
        brass.PlayScheduled(startTime);
        woods.PlayScheduled(startTime);
        perc.PlayScheduled(startTime);
    }

    void Prepare(AudioSource s)
    {
        if (!s) return;
        s.loop = true;
        s.playOnAwake = false;
        s.volume = 0f;
    }

    void Update()
    {
        UpdateDynamics();
        UpdateMix();
    }

    void UpdateDynamics()
    {
        Vector3 pos = wandTip.position;
        float speed = (pos - lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPos = pos;

        float target = Mathf.Clamp01(speed * dynamicsSensitivity);
        dynamics = Mathf.Lerp(dynamics, target, Time.deltaTime * smoothing);

        // Beat accent (simple ictus detection)
        if (speed > beatThreshold)
            accent = accentStrength;
    }

    void UpdateMix()
    {
        accent = Mathf.Lerp(accent, 0f, Time.deltaTime * 6f);

        float stringsVol = Mathf.Clamp01(dynamics + accent);
        float woodsVol   = Mathf.Clamp01(dynamics * 0.8f + accent * 0.6f);
        float brassVol   = Mathf.Clamp01(Mathf.InverseLerp(0.3f, 1f, dynamics) + accent);
        float percVol    = Mathf.Clamp01(Mathf.InverseLerp(0.2f, 1f, dynamics) + accent);

        if (strings) strings.volume = stringsVol;
        if (woods) woods.volume = woodsVol;
        if (brass) brass.volume = brassVol;
        if (perc) perc.volume = percVol;
    }
}