using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class ZenAmbientMusicController : MonoBehaviour
{
    [SerializeField] private AudioClip _ambientLoop;
    [SerializeField] private PlayerMusicStartDetector _startDetector;
    [SerializeField] private float _fadeOutDuration = 3.5f;
    [SerializeField] private float _startingVolume = 0.42f;
    [SerializeField] private bool _playOnStart = true;

    private AudioSource _audioSource;
    private bool _isFadingOut;
    private float _fadeVelocity;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = true;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = _startingVolume;

        if (_ambientLoop == null)
        {
            _ambientLoop = ZenAmbientLoopFactory.CreatePlaceholderAmbientClip();
        }

        _audioSource.clip = _ambientLoop;

        if (_startDetector == null)
        {
            _startDetector = FindFirstObjectByType<PlayerMusicStartDetector>();
        }
    }

    private void OnEnable()
    {
        if (_startDetector != null)
        {
            _startDetector.PlayerMusicStarted += BeginFadeOut;
        }
    }

    private void OnDisable()
    {
        if (_startDetector != null)
        {
            _startDetector.PlayerMusicStarted -= BeginFadeOut;
        }
    }

    private void Start()
    {
        if (_playOnStart && _audioSource.clip != null)
        {
            _audioSource.Play();
        }
    }

    private void Update()
    {
        if (!_isFadingOut)
        {
            return;
        }

        _audioSource.volume = Mathf.SmoothDamp(_audioSource.volume, 0f, ref _fadeVelocity, _fadeOutDuration * 0.35f);
        if (_audioSource.volume <= 0.005f)
        {
            _audioSource.volume = 0f;
            _audioSource.Stop();
            _isFadingOut = false;
        }
    }

    public void BeginFadeOut()
    {
        if (_audioSource == null || !_audioSource.isPlaying)
        {
            return;
        }

        _isFadingOut = true;
    }
}
